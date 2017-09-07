using System;
using System.Net.Sockets;
using System.Threading;

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

            var awaiter = awaitable.GetAwaiter();

            lock (awaiter.SyncRoot)
            {
                if (!awaiter.IsCompleted)
                    throw new InvalidOperationException(
                        "A socket operation is already in progress using the same awaitable SAEA.");

                awaiter.Reset();
                if (awaitable.ShouldCaptureContext)
                    awaiter.SyncContext = SynchronizationContext.Current;
            }

            try
            {
                if (!operation.Invoke(socket, awaitable))
                    awaiter.Complete();
            }
            catch (SocketException ex)
            {
                awaiter.Complete();
                awaitable.Saea.SocketError =
                    ex.SocketErrorCode != SocketError.Success ? ex.SocketErrorCode : SocketError.SocketError;
            }
            catch (Exception)
            {
                awaiter.Complete();
                awaitable.Saea.SocketError = SocketError.Success;
                throw;
            }

            return awaitable;
        }
    }
}
