using System;

namespace Cowboy.Sockets
{
    public class SaeaPool : QueuedObjectPool<SaeaAwaitable>
    {
        private Func<SaeaAwaitable> _saeaCreator;
        private Action<SaeaAwaitable> _saeaCleaner;

        public SaeaPool(int batchCount, int maxFreeCount, Func<SaeaAwaitable> saeaCreator, Action<SaeaAwaitable> saeaCleaner)
        {
            if (batchCount <= 0)
                throw new ArgumentOutOfRangeException("batchCount");
            if (maxFreeCount <= 0)
                throw new ArgumentOutOfRangeException("maxFreeCount");
            if (saeaCreator == null)
                throw new ArgumentNullException("saeaCreator");

            _saeaCreator = saeaCreator;
            _saeaCleaner = saeaCleaner;

            if (batchCount > maxFreeCount)
            {
                batchCount = maxFreeCount;
            }

            Initialize(batchCount, maxFreeCount);
        }

        public override bool Return(SaeaAwaitable saea)
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

        protected override void CleanupItem(SaeaAwaitable item)
        {
            try
            {
                item.Dispose();
            }
            catch (Exception) { }
        }

        protected override SaeaAwaitable Create()
        {
            return _saeaCreator();
        }
    }
}
