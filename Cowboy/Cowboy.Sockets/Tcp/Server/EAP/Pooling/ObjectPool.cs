using System;
using System.Collections.Concurrent;

namespace Cowboy.Sockets
{
    public abstract class ObjectPool<T> : IDisposable
    {
        private readonly ConcurrentBag<T> _bag;
        private readonly object _bagLock = new object();
        private bool _isDisposed;

        public ObjectPool()
        {
            _bag = new ConcurrentBag<T>();
        }

        protected abstract T Create();

        public int Count
        {
            get
            {
                return _bag.Count;
            }
        }

        public void Add(T item)
        {
            if (item == null)
                throw new ArgumentNullException("item");

            _bag.Add(item);
        }

        public T Take()
        {
            T item;
            return _bag.TryTake(out item) ? item : Create();
        }

        public bool IsDisposed
        {
            get
            {
                lock (_bagLock)
                {
                    return _isDisposed;
                }
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            lock (_bagLock)
            {
                if (_isDisposed)
                    return;

                if (disposing)
                {
                    // free managed objects here
                    for (int i = 0; i < this.Count; i++)
                    {
                        var item = this.Take() as IDisposable;
                        if (item != null)
                            item.Dispose();
                    }
                }

                _isDisposed = true;
            }
        }
    }
}
