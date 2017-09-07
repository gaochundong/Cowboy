using System;
using System.Net.Sockets;

namespace Cowboy.Sockets
{
    public sealed class SaeaAwaitable : IDisposable
    {
        private readonly object _sync = new object();
        private readonly SocketAsyncEventArgs _saea = new SocketAsyncEventArgs();
        private readonly SaeaAwaiter _awaiter;
        private bool _shouldCaptureContext;
        private bool _isDisposed;

        public SaeaAwaitable()
        {
            _awaiter = new SaeaAwaiter(this);
        }

        public SocketAsyncEventArgs Saea
        {
            get { return _saea; }
        }

        public object SyncRoot
        {
            get { return _sync; }
        }

        public SaeaAwaiter GetAwaiter()
        {
            return _awaiter;
        }

        public bool ShouldCaptureContext
        {
            get
            {
                return _shouldCaptureContext;
            }
            set
            {
                lock (_awaiter.SyncRoot)
                {
                    if (_awaiter.IsCompleted)
                        _shouldCaptureContext = value;
                    else
                        throw new InvalidOperationException(
                            "A socket operation is already in progress using the same awaitable SAEA.");
                }
            }
        }

        public bool IsDisposed
        {
            get { return _isDisposed; }
        }

        public void Dispose()
        {
            lock (SyncRoot)
            {
                if (!IsDisposed)
                {
                    _saea.Dispose();
                    _isDisposed = true;
                }
            }
        }
    }
}
