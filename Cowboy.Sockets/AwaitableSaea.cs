using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public sealed class AwaitableSaea : INotifyCompletion
    {
        private readonly static Action SENTINEL = () => { };

        internal bool _wasCompleted;
        internal Action _continuation;
        internal SocketAsyncEventArgs _saea;

        public AwaitableSaea(SocketAsyncEventArgs saea)
        {
            if (saea == null)
                throw new ArgumentNullException("saea");

            _saea = saea;

            _saea.Completed += delegate
            {
                var prev = _continuation ?? Interlocked.CompareExchange(ref _continuation, SENTINEL, null);
                if (prev != null)
                    prev();
            };
        }

        internal void Reset()
        {
            _wasCompleted = false;
            _continuation = null;
        }

        public AwaitableSaea GetAwaiter() { return this; }

        public bool IsCompleted { get { return _wasCompleted; } }

        public void OnCompleted(Action continuation)
        {
            if (_continuation == SENTINEL ||
                Interlocked.CompareExchange(ref _continuation, continuation, null) == SENTINEL)
            {
                Task.Run(continuation);
            }
        }

        public void GetResult()
        {
            if (_saea.SocketError != SocketError.Success)
                throw new SocketException((int)_saea.SocketError);
        }
    }

    public static class SocketExtensions
    {
        public static AwaitableSaea ReceiveAsync(this Socket socket, AwaitableSaea awaitable)
        {
            awaitable.Reset();
            if (!socket.ReceiveAsync(awaitable._saea))
                awaitable._wasCompleted = true;
            return awaitable;
        }

        public static AwaitableSaea SendAsync(this Socket socket, AwaitableSaea awaitable)
        {
            awaitable.Reset();
            if (!socket.SendAsync(awaitable._saea))
                awaitable._wasCompleted = true;
            return awaitable;
        }
    }
}
