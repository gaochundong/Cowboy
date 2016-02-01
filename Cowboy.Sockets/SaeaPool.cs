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
    public abstract class QueuedObjectPool<T>
    {
        private Queue<T> _objectQueue;
        private bool _isClosed;
        private int _batchAllocCount;
        private int _maxFreeCount;
        private readonly object _sync = new object();

        protected void Initialize(int batchAllocCount, int maxFreeCount)
        {
            if (batchAllocCount <= 0)
                throw new ArgumentOutOfRangeException("batchAllocCount");

            _batchAllocCount = batchAllocCount;
            _maxFreeCount = maxFreeCount;
            _objectQueue = new Queue<T>(batchAllocCount);
        }

        public virtual bool Return(T value)
        {
            lock (_sync)
            {
                if (_objectQueue.Count < _maxFreeCount && !_isClosed)
                {
                    _objectQueue.Enqueue(value);
                    return true;
                }

                return false;
            }
        }

        public T Take()
        {
            lock (_sync)
            {
                if (_objectQueue.Count == 0)
                {
                    AllocObjects();
                }

                return _objectQueue.Dequeue();
            }
        }

        public void Close()
        {
            lock (_sync)
            {
                foreach (T item in _objectQueue)
                {
                    if (item != null)
                    {
                        CleanupItem(item);
                    }
                }

                _objectQueue.Clear();
                _isClosed = true;
            }
        }

        protected virtual void CleanupItem(T item)
        {
        }

        protected abstract T Create();

        private void AllocObjects()
        {
            for (int i = 0; i < _batchAllocCount; i++)
            {
                _objectQueue.Enqueue(Create());
            }
        }
    }

    public class SaeaPool : QueuedObjectPool<SocketAsyncEventArgs>
    {
        private const int SingleBatchSize = 128 * 1024;
        private const int MaxBatchCount = 16;
        private const int MaxFreeCountFactor = 4;
        private int _acceptBufferSize;

        public SaeaPool(int acceptBufferSize)
        {
            if (acceptBufferSize <= 0)
                throw new ArgumentOutOfRangeException("acceptBufferSize");

            _acceptBufferSize = acceptBufferSize;
            int batchCount = (SingleBatchSize + _acceptBufferSize - 1) / _acceptBufferSize;
            if (batchCount > MaxBatchCount)
            {
                batchCount = MaxBatchCount;
            }

            Initialize(batchCount, batchCount * MaxFreeCountFactor);
        }

        public override bool Return(SocketAsyncEventArgs saea)
        {
            CleanupAcceptSocket(saea);

            if (!base.Return(saea))
            {
                CleanupItem(saea);
                return false;
            }

            return true;
        }

        internal static void CleanupAcceptSocket(SocketAsyncEventArgs saea)
        {
            var socket = saea.AcceptSocket;
            if (socket != null)
            {
                saea.AcceptSocket = null;

                try
                {
                    socket.Close(0);
                }
                catch (SocketException ex)
                {
                }
                catch (ObjectDisposedException ex)
                {
                }
            }
        }

        protected override void CleanupItem(SocketAsyncEventArgs item)
        {
            item.Dispose();
        }

        protected override SocketAsyncEventArgs Create()
        {
            var saea = new SocketAsyncEventArgs();

            var acceptBuffer = AllocateByteArray(_acceptBufferSize);
            saea.SetBuffer(acceptBuffer, 0, _acceptBufferSize);

            return saea;
        }

        private static byte[] AllocateByteArray(int size)
        {
            try
            {
                // Safe to catch OOM from this as long as the ONLY thing it does 
                // is a simple allocation of a primitive type (no method calls).
                return new byte[size];
            }
            catch (OutOfMemoryException exception)
            {
                // Convert OOM into an exception that can be safely handled by higher layers.
                throw new InsufficientMemoryException(
                    string.Format("Buffer allocation failed for size [{0}].", size), exception);
            }
        }
    }
}
