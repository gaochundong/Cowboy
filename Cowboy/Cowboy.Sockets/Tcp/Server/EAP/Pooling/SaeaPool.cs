using System;

namespace Cowboy.Sockets
{
    public class SaeaPool : ObjectPool<SaeaAwaitable>
    {
        private Func<SaeaAwaitable> _createSaea;
        private Action<SaeaAwaitable> _cleanSaea;

        public SaeaPool(Func<SaeaAwaitable> createSaea, Action<SaeaAwaitable> cleanSaea)
            : base()
        {
            if (createSaea == null)
                throw new ArgumentNullException("createSaea");
            if (cleanSaea == null)
                throw new ArgumentNullException("cleanSaea");

            _createSaea = createSaea;
            _cleanSaea = cleanSaea;
        }

        public SaeaPool Initialize(int initialCount = 0)
        {
            if (initialCount < 0)
                throw new ArgumentOutOfRangeException(
                    "initialCount",
                    initialCount,
                    "Initial count must not be less than zero.");

            for (int i = 0; i < initialCount; i++)
            {
                Add(Create());
            }

            return this;
        }

        protected override SaeaAwaitable Create()
        {
            return _createSaea();
        }

        public void Return(SaeaAwaitable saea)
        {
            _cleanSaea(saea);
            Add(saea);
        }
    }
}
