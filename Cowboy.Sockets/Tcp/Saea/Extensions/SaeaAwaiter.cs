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
    public sealed class SaeaAwaiter : INotifyCompletion
    {
        private static readonly Action SENTINEL = delegate { };
        private readonly SaeaAwaitable _awaitable;
        private readonly object _sync = new object();
        private Action _continuation;
        private bool _isCompleted = true;
        private SynchronizationContext _syncCtx;

        internal SaeaAwaiter(SaeaAwaitable awaitable)
        {
            _awaitable = awaitable;
            _awaitable.Saea.Completed += delegate
            {
                var continuation = _continuation
                    ?? Interlocked.CompareExchange(ref _continuation, SENTINEL, null);

                if (continuation != null)
                {
                    var syncContext = _awaitable.ShouldCaptureContext
                        ? SyncContext
                        : null;

                    Complete();
                    if (syncContext != null)
                        syncContext.Post(s => continuation(), null);
                    else
                        continuation();
                }
            };
        }

        internal object SyncRoot
        {
            get { return _sync; }
        }

        internal SynchronizationContext SyncContext
        {
            get { return _syncCtx; }
            set { _syncCtx = value; }
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
                if (!_awaitable.ShouldCaptureContext)
                    Task.Run(continuation);
                else
                    Task.Factory.StartNew(
                        continuation,
                        CancellationToken.None,
                        TaskCreationOptions.DenyChildAttach,
                        TaskScheduler.FromCurrentSynchronizationContext());
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
                //var buffer = _awaitable.Buffer;
                //_awaitable.Transferred =
                //    buffer.Count == 0
                //    ? buffer
                //    : new ArraySegment<byte>(
                //        buffer.Array,
                //        buffer.Offset,
                //        _awaitable.Saea.BytesTransferred);

                if (_awaitable.ShouldCaptureContext)
                    _syncCtx = null;

                _isCompleted = true;
            }
        }

        internal void Reset()
        {
            _awaitable.Saea.AcceptSocket = null;
            _awaitable.Saea.SocketError = SocketError.AlreadyInProgress;
            //_awaitable.Transferred = SaeaAwaitable.EmptyArraySegment;
            _isCompleted = false;
            _continuation = null;
        }
    }
}
