using System;
using System.Collections.Generic;

namespace Cowboy.Buffer
{
    public class FixedByteBufferManager
    {
        private byte[] _bigBuffer;
        private Stack<ArraySegment<byte>> _segments;
        private readonly object _sync = new object();

        public FixedByteBufferManager(int bufferCount, int bufferSize)
        {
            BufferCount = bufferCount;
            BufferSize = bufferSize;

            Initialize();
        }

        private void Initialize()
        {
            int bytesToAllocate = BufferCount * BufferSize;
            _bigBuffer = new byte[bytesToAllocate];

            _segments = new Stack<ArraySegment<byte>>(BufferCount);
            for (int i = BufferCount - 1; i >= 0; i--)
            {
                var segment = new ArraySegment<byte>(_bigBuffer, i * BufferSize, BufferSize);
                _segments.Push(segment);
            }
        }

        public int BufferCount { get; private set; }

        public int BufferSize { get; private set; }

        public ArraySegment<byte> BorrowBuffer()
        {
            lock (_sync)
            {
                return _segments.Pop();
            }
        }

        public void ReturnBuffer(ArraySegment<byte> segment)
        {
            if (segment == null)
                throw new ArgumentNullException("segment");

            lock (_sync)
            {
                _segments.Push(segment);
            }
        }

        public void ReturnBuffers(IEnumerable<ArraySegment<byte>> segments)
        {
            if (segments != null)
            {
                foreach (var segment in segments)
                {
                    ReturnBuffer(segment);
                }
            }
        }

        public void ReturnBuffers(params ArraySegment<byte>[] segments)
        {
            if (segments != null)
            {
                foreach (var segment in segments)
                {
                    ReturnBuffer(segment);
                }
            }
        }
    }
}
