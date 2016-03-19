using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;

namespace RioSharp
{
    public abstract class RioConnectionOrientedSocketPool : RioSocketPool
    {
        protected IntPtr socketIocp;
        internal RioConnectionOrientedSocket[] allSockets;

        public unsafe RioConnectionOrientedSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount, ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
            : base(sendPool, revicePool, adressFam, sockType, protocol, maxOutstandingReceive, maxOutstandingSend, maxConnections)
        {
            var adrSize = (sizeof(sockaddr_in) + 16) * 2;
            var overlapped = Marshal.AllocHGlobal(new IntPtr(socketCount * Marshal.SizeOf<RioNativeOverlapped>()));
            var adressBuffer = Marshal.AllocHGlobal(new IntPtr(socketCount * adrSize));

            allSockets = new RioConnectionOrientedSocket[socketCount];

            for (int i = 0; i < socketCount; i++)
            {
                allSockets[i] = new RioConnectionOrientedSocket(overlapped + (i * Marshal.SizeOf<RioNativeOverlapped>()), adressBuffer + (i * adrSize), this, SendBufferPool, ReceiveBufferPool, maxOutstandingReceive, maxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue, adressFam, sockType, protocol);
                allSockets[i]._overlapped->SocketIndex = i;
            }

            if ((socketIocp = Kernel32.CreateIoCompletionPort((IntPtr)(-1), IntPtr.Zero, 0, 1)) == IntPtr.Zero)
                Kernel32.ThrowLastError();

            foreach (var s in allSockets)
            {
                if ((Kernel32.CreateIoCompletionPort(s.Socket, socketIocp, 0, 1)) == IntPtr.Zero)
                    Kernel32.ThrowLastError();
            }

            Thread SocketIocpThread = new Thread(SocketIocpComplete);
            SocketIocpThread.IsBackground = true;
            SocketIocpThread.Start();
        }

        protected abstract void SocketIocpComplete(object o);

        internal unsafe virtual void Recycle(RioConnectionOrientedSocket socket)
        {
            RioSocket c;
            activeSockets.TryRemove(socket.GetHashCode(), out c);
            socket.ResetOverlapped();
            socket._overlapped->Status = 1;
            if (!RioStatic.DisconnectEx(socket.Socket, socket._overlapped, 0x02, 0)) //TF_REUSE_SOCKET
                if (WinSock.WSAGetLastError() != 997) // error_io_pending
                    WinSock.ThrowLastWSAError();
            //else
            //    AcceptEx(socket);
        }

        public override void Dispose()
        {
            Kernel32.CloseHandle(socketIocp);
            for (int i = 0; i < allSockets.Length; i++)
                allSockets[i].Dispose();

            base.Dispose();
        }
    }
}
