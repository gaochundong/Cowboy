using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public static class SaeaExtensions
    {
        private static readonly Func<Socket, SaeaAwaitable, bool> ACCEPT = (s, a) => s.AcceptAsync(a.Saea);
        private static readonly Func<Socket, SaeaAwaitable, bool> CONNECT = (s, a) => s.ConnectAsync(a.Saea);
        private static readonly Func<Socket, SaeaAwaitable, bool> DISCONNECT = (s, a) => s.DisconnectAsync(a.Saea);
        private static readonly Func<Socket, SaeaAwaitable, bool> RECEIVE = (s, a) => s.ReceiveAsync(a.Saea);
        private static readonly Func<Socket, SaeaAwaitable, bool> SEND = (s, a) => s.SendAsync(a.Saea);

        public static SaeaAwaitable AcceptAsync(this Socket socket, SaeaAwaitable awaitable)
        {
            return OperateAsync(socket, awaitable, ACCEPT);
        }

        public static SaeaAwaitable ConnectAsync(this Socket socket, SaeaAwaitable awaitable)
        {
            return OperateAsync(socket, awaitable, CONNECT);
        }

        public static SaeaAwaitable DisonnectAsync(this Socket socket, SaeaAwaitable awaitable)
        {
            return OperateAsync(socket, awaitable, DISCONNECT);
        }

        public static SaeaAwaitable ReceiveAsync(this Socket socket, SaeaAwaitable awaitable)
        {
            return OperateAsync(socket, awaitable, RECEIVE);
        }

        public static SaeaAwaitable SendAsync(this Socket socket, SaeaAwaitable awaitable)
        {
            return OperateAsync(socket, awaitable, SEND);
        }

        private static SaeaAwaitable OperateAsync(Socket socket, SaeaAwaitable awaitable, Func<Socket, SaeaAwaitable, bool> operation)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            if (awaitable == null)
                throw new ArgumentNullException("awaitable");

            var a = awaitable.GetAwaiter();

            lock (a.SyncRoot)
            {
                if (!a.IsCompleted)
                    throw new InvalidOperationException(
                        "A socket operation is already in progress using the same await-able SAEA.");

                a.Reset();
                if (awaitable.ShouldCaptureContext)
                    a.SyncContext = SynchronizationContext.Current;
            }

            try
            {
                if (!operation.Invoke(socket, awaitable))
                    a.Complete();
            }
            catch (SocketException x)
            {
                a.Complete();
                awaitable.Saea.SocketError = x.SocketErrorCode != SocketError.Success
                    ? x.SocketErrorCode
                    : SocketError.SocketError;
            }
            catch (Exception)
            {
                a.Complete();
                awaitable.Saea.SocketError = SocketError.Success;
                throw;
            }

            return awaitable;
        }
    }
}
