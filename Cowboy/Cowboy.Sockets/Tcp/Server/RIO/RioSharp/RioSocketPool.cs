using System;
using System.Collections.Concurrent;
using System.Threading;

namespace RioSharp
{
    public abstract class RioSocketPool : IDisposable
    {
        internal RioFixedBufferPool SendBufferPool, ReceiveBufferPool;
        IntPtr _sendBufferId, _reciveBufferId;
        IntPtr SendCompletionPort, ReceiveCompletionPort;
        protected IntPtr SendCompletionQueue, ReceiveCompletionQueue;
        protected uint MaxOutstandingReceive, MaxOutstandingSend, MaxOutsandingCompletions;
        internal ConcurrentDictionary<long, RioSocket> activeSockets = new ConcurrentDictionary<long, RioSocket>();
        protected ADDRESS_FAMILIES adressFam;
        protected SOCKET_TYPE sockType;
        protected PROTOCOL protocol;


        public unsafe RioSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool receivePool, ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxOutsandingCompletions = 2048)
        {
            MaxOutstandingReceive = maxOutstandingReceive;
            MaxOutstandingSend = maxOutstandingSend;
            MaxOutsandingCompletions = maxOutsandingCompletions;
            SendBufferPool = sendPool;
            ReceiveBufferPool = receivePool;

            this.adressFam = adressFam;
            this.sockType = sockType;
            this.protocol = protocol;

            var version = new Version(2, 2);
            WSAData data;
            var result = WinSock.WSAStartup((short)version.Raw, out data);
            if (result != 0)
                WinSock.ThrowLastWSAError();

            RioStatic.Initalize();

            if ((ReceiveCompletionPort = Kernel32.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Kernel32.ThrowLastError();

            if ((SendCompletionPort = Kernel32.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Kernel32.ThrowLastError();


            _sendBufferId = RioStatic.RegisterBuffer(SendBufferPool.BufferPointer, (uint)SendBufferPool.TotalLength);
            WinSock.ThrowLastWSAError();
            SendBufferPool.SetBufferId(_sendBufferId);

            _reciveBufferId = RioStatic.RegisterBuffer(ReceiveBufferPool.BufferPointer, (uint)ReceiveBufferPool.TotalLength);
            WinSock.ThrowLastWSAError();
            ReceiveBufferPool.SetBufferId(_reciveBufferId);

            var sendCompletionMethod = new RIO_NOTIFICATION_COMPLETION()
            {
                Type = RIO_NOTIFICATION_COMPLETION_TYPE.IOCP_COMPLETION,
                Iocp = new RIO_NOTIFICATION_COMPLETION_IOCP()
                {
                    IocpHandle = SendCompletionPort,
                    QueueCorrelation = 0,
                    Overlapped = (NativeOverlapped*)-1
                }
            };

            if ((SendCompletionQueue = RioStatic.CreateCompletionQueue(MaxOutsandingCompletions, sendCompletionMethod)) == IntPtr.Zero)
                WinSock.ThrowLastWSAError();

            var receiveCompletionMethod = new RIO_NOTIFICATION_COMPLETION()
            {
                Type = RIO_NOTIFICATION_COMPLETION_TYPE.IOCP_COMPLETION,
                Iocp = new RIO_NOTIFICATION_COMPLETION_IOCP()
                {
                    IocpHandle = ReceiveCompletionPort,
                    QueueCorrelation = 0,
                    Overlapped = (NativeOverlapped*)-1
                }
            };

            if ((ReceiveCompletionQueue = RioStatic.CreateCompletionQueue(MaxOutsandingCompletions, receiveCompletionMethod)) == IntPtr.Zero)
                WinSock.ThrowLastWSAError();


            Thread receiveThread = new Thread(ProcessReceiveCompletes);
            receiveThread.IsBackground = true;
            receiveThread.Start();
            Thread sendThread = new Thread(ProcessSendCompletes);
            sendThread.IsBackground = true;
            sendThread.Start();

        }

        public unsafe RioBufferSegment PreAllocateWrite(byte[] buffer)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                Buffer.MemoryCopy(p, currentSegment.RawPointer, currentSegment.TotalLength, buffer.Length);
            }

            currentSegment.SegmentPointer->Length = buffer.Length;
            currentSegment.AutoFree = false;
            return currentSegment;
        }

        unsafe void ProcessReceiveCompletes(object o)
        {
            uint maxResults = Math.Min(MaxOutstandingReceive, int.MaxValue);
            RIO_RESULT* results = stackalloc RIO_RESULT[(int)maxResults];
            RioSocket connection;
            uint count;
            IntPtr key, bytes;
            NativeOverlapped* overlapped = stackalloc NativeOverlapped[1];
            RIO_RESULT result;
            RioBufferSegment buf;

            while (true)
            {
                RioStatic.Notify(ReceiveCompletionQueue);
                WinSock.ThrowLastWSAError();

                if (Kernel32.GetQueuedCompletionStatus(ReceiveCompletionPort, out bytes, out key, out overlapped, -1) != 0)
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(ReceiveCompletionQueue, (IntPtr)results, maxResults);
                        if (count == 0xFFFFFFFF)
                            WinSock.ThrowLastWSAError();

                        for (var i = 0; i < count; i++)
                        {
                            result = results[i];
                            buf = ReceiveBufferPool.AllSegments[result.RequestCorrelation];
                            if (activeSockets.TryGetValue(result.ConnectionCorrelation, out connection))
                            {
                                buf.SegmentPointer->Length = (int)result.BytesTransferred;
                                connection.onIncommingSegment(connection, buf);
                            }
                            else
                                buf.Dispose();
                        }

                    } while (count > 0);
                }
                else
                    Kernel32.ThrowLastError();
            }
        }

        unsafe void ProcessSendCompletes(object o)
        {
            uint maxResults = Math.Min(MaxOutstandingSend, int.MaxValue);
            RIO_RESULT* results = stackalloc RIO_RESULT[(int)maxResults];
            uint count;
            IntPtr key, bytes;
            NativeOverlapped* overlapped = stackalloc NativeOverlapped[1];

            while (true)
            {
                RioStatic.Notify(SendCompletionQueue);
                if (Kernel32.GetQueuedCompletionStatus(SendCompletionPort, out bytes, out key, out overlapped, -1) != 0)
                {
                    do
                    {
                        count = RioStatic.DequeueCompletion(SendCompletionQueue, (IntPtr)results, maxResults);
                        if (count == 0xFFFFFFFF)
                            WinSock.ThrowLastWSAError();
                        for (var i = 0; i < count; i++)
                        {
                            var buf = SendBufferPool.AllSegments[results[i].RequestCorrelation];
                            if (buf.AutoFree)
                                buf.Dispose();
                        }

                    } while (count > 0);
                }
                else
                    Kernel32.ThrowLastError();
            }
        }

        public virtual void Dispose()
        {
            RioStatic.DeregisterBuffer(_sendBufferId);
            RioStatic.DeregisterBuffer(_reciveBufferId);

            Kernel32.CloseHandle(SendCompletionPort);
            Kernel32.CloseHandle(ReceiveCompletionPort);
            RioStatic.CloseCompletionQueue(SendCompletionQueue);
            RioStatic.CloseCompletionQueue(ReceiveCompletionQueue);

            WinSock.WSACleanup();

            SendBufferPool.Dispose();
            ReceiveBufferPool.Dispose();
        }
    }
}
