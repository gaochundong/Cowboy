using System;
using System.Net.Sockets;

namespace Cowboy.Sockets
{
    public sealed class SaeaAwaitable : IDisposable
    {
        private readonly object _sync = new object();
        private readonly SocketAsyncEventArgs _saea = new SocketAsyncEventArgs();
        private readonly SaeaAwaiter _awaiter;
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
