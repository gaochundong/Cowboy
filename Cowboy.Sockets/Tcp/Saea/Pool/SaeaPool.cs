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
    public class SaeaPool : QueuedObjectPool<SocketAsyncEventArgs>
    {
        private Func<SocketAsyncEventArgs> _saeaCreator;
        private Action<SocketAsyncEventArgs> _saeaCleaner;

        public SaeaPool(int batchCount, int maxFreeCount, Func<SocketAsyncEventArgs> saeaCreator, Action<SocketAsyncEventArgs> saeaCleaner)
        {
            if (batchCount <= 0)
                throw new ArgumentOutOfRangeException("batchCount");
            if (maxFreeCount <= 0)
                throw new ArgumentOutOfRangeException("maxFreeCount");

            _saeaCreator = saeaCreator;
            _saeaCleaner = saeaCleaner;

            if (batchCount > maxFreeCount)
            {
                batchCount = maxFreeCount;
            }

            Initialize(batchCount, maxFreeCount);
        }

        public override bool Return(SocketAsyncEventArgs saea)
        {
            if (_saeaCleaner != null)
            {
                _saeaCleaner(saea);
            }

            if (!base.Return(saea))
            {
                CleanupItem(saea);
                return false;
            }

            return true;
        }

        protected override void CleanupItem(SocketAsyncEventArgs item)
        {
            item.Dispose();
        }

        protected override SocketAsyncEventArgs Create()
        {
            if (_saeaCreator == null)
            {
                return new SocketAsyncEventArgs();
            }
            else
            {
                return _saeaCreator();
            }
        }
    }
}
