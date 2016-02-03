using System;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public sealed class SaeaAwaiter : INotifyCompletion
    {
        private static readonly Action SENTINEL = delegate { };
        private readonly SaeaAwaitable _awaitable;
        private readonly object _sync = new object();
        private Action _continuation;
        private bool _isCompleted = true;

        public SaeaAwaiter(SaeaAwaitable awaitable)
        {
            _awaitable = awaitable;
            _awaitable.Saea.Completed += delegate
            {
                var continuation = _continuation ?? Interlocked.CompareExchange(ref _continuation, SENTINEL, null);

                if (continuation != null)
                {
                    Complete();

                    if (continuation != SENTINEL)
                        Task.Run(continuation);
                }
            };
        }

        public object SyncRoot
        {
            get { return _sync; }
        }

        public SocketError GetResult()
        {
            return _awaitable.Saea.SocketError;
        }

        void INotifyCompletion.OnCompleted(Action continuation)
        {
            if (_continuation == SENTINEL
                || Interlocked.CompareExchange(ref _continuation, continuation, null) == SENTINEL)
            {
                Complete();

                Task.Run(continuation);
            }
        }

        public bool IsCompleted
        {
            get { return _isCompleted; }
        }

        internal void Complete()
        {
            if (!IsCompleted)
            {
                _isCompleted = true;
            }
        }

        internal void Reset()
        {
            _isCompleted = false;
            _continuation = null;
        }
    }
}
