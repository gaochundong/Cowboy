using System;
using System.Threading;

namespace Cowboy.Sockets.Experimental
{
    public unsafe class RioSocketBase : IDisposable
    {
        IntPtr _requestQueue;
        internal IntPtr Socket;
        internal RioFixedBufferPool SendBufferPool, ReceiveBufferPool;
        internal Action<RioBufferSegment> onIncommingSegment = s => { };

        internal RioSocketBase(RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool,
            uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue,
            ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol)
        {
            if ((Socket = WinSock.WSASocket(adressFam, sockType, protocol, IntPtr.Zero, 0, SOCKET_FLAGS.REGISTERED_IO | SOCKET_FLAGS.WSA_FLAG_OVERLAPPED)) == IntPtr.Zero)
                WinSock.ThrowLastWSAError();

            SendBufferPool = sendBufferPool;
            ReceiveBufferPool = receiveBufferPool;

            _requestQueue = RioStatic.CreateRequestQueue(Socket, maxOutstandingReceive, 1, maxOutstandingSend, 1, ReceiveCompletionQueue, SendCompletionQueue, GetHashCode());
            WinSock.ThrowLastWSAError();
        }


        public Action<RioBufferSegment> OnIncommingSegment
        {
            get
            {
                return onIncommingSegment;
            }
            set
            {
                onIncommingSegment = value ?? (s => { });
            }
        }


        public void WritePreAllocated(RioBufferSegment Segment)
        {
            unsafe
            {
                if (!RioStatic.Send(_requestQueue, Segment.SegmentPointer, 1, RIO_SEND_FLAGS.DEFER, Segment.Index))
                    WinSock.ThrowLastWSAError();
            }
        }

        internal unsafe void CommitSend()
        {
            if (!RioStatic.Send(_requestQueue, RIO_BUFSEGMENT.NullSegment, 0, RIO_SEND_FLAGS.COMMIT_ONLY, 0))
                WinSock.ThrowLastWSAError();
        }


        internal unsafe void SendInternal(RioBufferSegment segment, RIO_SEND_FLAGS flags)
        {
            if (!RioStatic.Send(_requestQueue, segment.SegmentPointer, 1, flags, segment.Index))
                WinSock.ThrowLastWSAError();
        }

        internal unsafe void ReciveInternal()
        {
            RioBufferSegment buf;
            if (ReceiveBufferPool.TryGetBuffer(out buf))
            {
                if (!RioStatic.Receive(_requestQueue, buf.SegmentPointer, 1, RIO_RECEIVE_FLAGS.NONE, buf.Index))
                    WinSock.ThrowLastWSAError();
            }
            else
                ThreadPool.QueueUserWorkItem(o =>
                {
                    var b = ReceiveBufferPool.GetBuffer();
                    if (!RioStatic.Receive(_requestQueue, ReceiveBufferPool.GetBuffer().SegmentPointer, 1, RIO_RECEIVE_FLAGS.NONE, b.Index))
                        WinSock.ThrowLastWSAError();
                }, null);
        }

        public unsafe void WriteFixed(byte[] buffer)
        {
            var currentSegment = SendBufferPool.GetBuffer();
            fixed (byte* p = &buffer[0])
            {
                System.Buffer.MemoryCopy(p, currentSegment.RawPointer, currentSegment.TotalLength, buffer.Length);
            }
            currentSegment.SegmentPointer->Length = buffer.Length;
            SendInternal(currentSegment, RIO_SEND_FLAGS.NONE);
        }

        public virtual void Dispose()
        {
            WinSock.closesocket(Socket);
        }
    }
}

