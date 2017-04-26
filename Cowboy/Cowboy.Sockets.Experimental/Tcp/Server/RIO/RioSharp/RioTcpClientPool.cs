using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioTcpClientPool : RioConnectionOrientedSocketPool
    {
        ConcurrentQueue<RioConnectionOrientedSocket> _freeSockets = new ConcurrentQueue<RioConnectionOrientedSocket>();
        ConcurrentDictionary<RioConnectionOrientedSocket, TaskCompletionSource<RioSocket>> _ongoingConnections = new ConcurrentDictionary<RioConnectionOrientedSocket, TaskCompletionSource<RioSocket>>();

        public RioTcpClientPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, uint socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024)
            : base(sendPool, revicePool, socketCount, ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP, maxOutstandingReceive, maxOutstandingSend, (maxOutstandingReceive + maxOutstandingSend) * socketCount)
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
                sa.sin_family = adressFam;
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
            TaskCompletionSource<RioSocket> r;
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

        public unsafe Task<RioSocket> Connect(Uri adress)
        {
            var adr = Dns.GetHostAddressesAsync(adress.Host).Result.First(i => i.AddressFamily == AddressFamily.InterNetwork);

            in_addr inAddress = new in_addr();
            inAddress.s_b1 = adr.GetAddressBytes()[0];
            inAddress.s_b2 = adr.GetAddressBytes()[1];
            inAddress.s_b3 = adr.GetAddressBytes()[2];
            inAddress.s_b4 = adr.GetAddressBytes()[3];

            sockaddr_in sa = new sockaddr_in();
            sa.sin_family = adressFam;
            sa.sin_port = WinSock.htons((ushort)adress.Port);
            //Imports.ThrowLastWSAError();
            sa.sin_addr = inAddress;

            RioConnectionOrientedSocket s;
            _freeSockets.TryDequeue(out s);
            var tcs = new TaskCompletionSource<RioSocket>();
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
