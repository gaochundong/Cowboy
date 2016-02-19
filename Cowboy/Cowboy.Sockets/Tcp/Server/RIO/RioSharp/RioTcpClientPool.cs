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
using System.Collections.Concurrent;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace Cowboy.Sockets.Experimental
{
    public class RioTcpClientPool : RioConnectionOrientedSocketPool
    {
        ConcurrentQueue<RioConnectionOrientedSocket> _freeSockets = new ConcurrentQueue<RioConnectionOrientedSocket>();
        ConcurrentDictionary<RioConnectionOrientedSocket, TaskCompletionSource<RioConnectionOrientedSocket>> _ongoingConnections = new ConcurrentDictionary<RioConnectionOrientedSocket, TaskCompletionSource<RioConnectionOrientedSocket>>();

        public RioTcpClientPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024)
            : base(sendPool, revicePool, socketCount, maxOutstandingReceive, maxOutstandingSend, (maxOutstandingReceive + maxOutstandingSend) * socketCount)
        {
            foreach (var s in allSockets)
            {
                _freeSockets.Enqueue(s);

                in_addr inAddress = new in_addr();
                inAddress.s_b1 = 0;
                inAddress.s_b2 = 0;
                inAddress.s_b3 = 0;
                inAddress.s_b4 = 0;

                sockaddr_in sa = new sockaddr_in();
                sa.sin_family = ADDRESS_FAMILIES.AF_INET;
                sa.sin_port = 0;
                //Imports.ThrowLastWSAError();
                sa.sin_addr = inAddress;

                unsafe
                {
                    if (WinSock.bind(s.Socket, ref sa, sizeof(sockaddr_in)) == WinSock.SOCKET_ERROR)
                        WinSock.ThrowLastWSAError();
                }
            }
        }


        protected override unsafe void SocketIocpComplete(object o)
        {
            IntPtr lpNumberOfBytes;
            IntPtr lpCompletionKey;
            RioNativeOverlapped* lpOverlapped = stackalloc RioNativeOverlapped[1];
            TaskCompletionSource<RioConnectionOrientedSocket> r;
            RioConnectionOrientedSocket res;
            int lpcbTransfer;
            int lpdwFlags;

            while (true)
            {
                if (Kernel32.GetQueuedCompletionStatusRio(socketIocp, out lpNumberOfBytes, out lpCompletionKey, out lpOverlapped, -1))
                {
                    if (lpOverlapped->Status == 1)
                    {
                        _freeSockets.Enqueue(allSockets[lpOverlapped->SocketIndex]);
                    }
                    else if (lpOverlapped->Status == 2)
                    {
                        if (WinSock.WSAGetOverlappedResult(allSockets[lpOverlapped->SocketIndex].Socket, lpOverlapped, out lpcbTransfer, false, out lpdwFlags))
                        {
                            res = allSockets[lpOverlapped->SocketIndex];
                            activeSockets.TryAdd(res.GetHashCode(), res);
                            if (_ongoingConnections.TryRemove(res, out r))
                                r.SetResult(res);
                        }
                        else {
                            //recycle socket
                        }
                    }
                } //1225
                else
                {
                    var error = Marshal.GetLastWin32Error();

                    if (error != 0 && error != 64 & error != 1225) //connection no longer available
                        throw new Win32Exception(error);
                    else
                    {
                        res = allSockets[lpOverlapped->SocketIndex];
                        _freeSockets.Enqueue(allSockets[lpOverlapped->SocketIndex]);
                        if (_ongoingConnections.TryRemove(res, out r))
                            r.SetException(new Win32Exception(error));
                    }
                }
            }
        }

        public unsafe Task<RioConnectionOrientedSocket> Connect(Uri adress)
        {
            var adr = Dns.GetHostAddressesAsync(adress.Host).Result.First(i => i.AddressFamily == AddressFamily.InterNetwork);

            in_addr inAddress = new in_addr();
            inAddress.s_b1 = adr.GetAddressBytes()[0];
            inAddress.s_b2 = adr.GetAddressBytes()[1];
            inAddress.s_b3 = adr.GetAddressBytes()[2];
            inAddress.s_b4 = adr.GetAddressBytes()[3];

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = ADDRESS_FAMILIES.AF_INET;
            sa.sin_port = WinSock.htons((ushort)adress.Port);
            //Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            RioConnectionOrientedSocket s;
            _freeSockets.TryDequeue(out s);
            var tcs = new TaskCompletionSource<RioConnectionOrientedSocket>();
            _ongoingConnections.TryAdd(s, tcs);

            uint gurka;

            unsafe
            {
                s.ResetOverlapped();
                s._overlapped->Status = 2;
                if (!RioStatic.ConnectEx(s.Socket, sa, sizeof(sockaddr_in), IntPtr.Zero, 0, out gurka, s._overlapped))
                    if (WinSock.WSAGetLastError() != 997) // error_io_pending
                        WinSock.ThrowLastWSAError();
            }

            return tcs.Task;
        }
    }
}
