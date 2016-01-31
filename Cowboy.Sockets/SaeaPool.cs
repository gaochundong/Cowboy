using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    internal sealed class SaeaPool
    {
        private ConcurrentStack<SocketAsyncEventArgs> _pool;

        public SaeaPool()
        {
            _pool = new ConcurrentStack<SocketAsyncEventArgs>();
        }

        public int Count
        {
            get { return _pool.Count; }
        }

        public bool IsEmpty
        {
            get { return _pool.IsEmpty; }
        }

        public bool TryPop(out SocketAsyncEventArgs saea)
        {
            return _pool.TryPop(out saea);
        }

        public void Push(SocketAsyncEventArgs saea)
        {
            if (saea == null)
                throw new ArgumentNullException("saea");

            _pool.Push(saea);
        }
    }
}
