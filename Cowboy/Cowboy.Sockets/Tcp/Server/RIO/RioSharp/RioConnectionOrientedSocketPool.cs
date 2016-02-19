// The MIT License (MIT)
// 
// Copyright (c) 2015 Allan Lindqvist
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace Cowboy.Sockets.Experimental
{
    public abstract class RioConnectionOrientedSocketPool : RioSocketPool
    {
        protected IntPtr socketIocp;
        internal RioConnectionOrientedSocket[] allSockets;

        public unsafe RioConnectionOrientedSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxConnections = 1024)
            : base(sendPool, revicePool, maxOutstandingReceive, maxOutstandingSend, maxConnections)
        {
            var adrSize = (sizeof(sockaddr_in) + 16) * 2;
            var overlapped = Marshal.AllocHGlobal(new IntPtr(socketCount * Marshal.SizeOf<RioNativeOverlapped>()));
            var adressBuffer = Marshal.AllocHGlobal(new IntPtr(socketCount * adrSize));

            allSockets = new RioConnectionOrientedSocket[socketCount];

            for (int i = 0; i < socketCount; i++)
            {
                allSockets[i] = new RioConnectionOrientedSocket(overlapped + (i * Marshal.SizeOf<RioNativeOverlapped>()), adressBuffer + (i * adrSize), this, SendBufferPool, ReceiveBufferPool, maxOutstandingReceive, maxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue);
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

        protected abstract unsafe void SocketIocpComplete(object o);

        internal unsafe virtual void Recycle(RioConnectionOrientedSocket socket)
        {
            RioSocketBase c;
            activeSockets.TryRemove(socket.GetHashCode(), out c);
            socket.ResetOverlapped();
            socket._overlapped->Status = 1;
            if (!RioStatic.DisconnectEx(c.Socket, socket._overlapped, 0x02, 0)) //TF_REUSE_SOCKET
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
