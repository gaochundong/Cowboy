using System;
using System.Collections.Generic;

namespace Cowboy.WebSockets.Buffer
{
    public class GrowingByteBufferManager : IBufferManager
    {
        public int _bufferCount;
        public int _bufferSize;
        private Stack<byte[]> _bufferStack;
        private readonly object _sync = new object();
        private double _autoExpandedScale = 1.5;

        public GrowingByteBufferManager(int initialPooledBufferCount, int bufferSize)
        {
            _bufferCount = initialPooledBufferCount;
            _bufferSize = bufferSize;

            AutoExpanded = true;

            Initialize();
        }

        private void Initialize()
        {
            _bufferStack = new Stack<byte[]>(_bufferCount);

            for (int i = 0; i < _bufferCount; i++)
            {
                byte[] buffer = new byte[_bufferSize];
                _bufferStack.Push(buffer);
            }
        }

        public int BufferCount { get { return _bufferCount; } }
        public int BufferSize { get { return _bufferSize; } }
        public bool AutoExpanded { get; set; }

        public double AutoExpandedScale
        {
            get { return _autoExpandedScale; }
            set
            {
                if (value <= 1)
                    throw new ArgumentException("Auto expanded scale must be greater than 1.");
                _autoExpandedScale = value;
            }
        }

        public int BufferRemaning
        {
            get
            {
                lock (_sync)
                {
                    return _bufferStack.Count;
                }
            }
        }

        private void ExpandBufferStack()
        {
            int currentBufferCount = _bufferCount;
            _bufferCount = (int)(_bufferCount * AutoExpandedScale);

            for (int i = 0; i < _bufferCount - currentBufferCount; i++)
            {
                var buffer = new byte[_bufferSize];
                _bufferStack.Push(buffer);
            }
        }

        public byte[] BorrowBuffer()
        {
            lock (_sync)
            {
                if (_bufferStack.Count > 0)
                    return _bufferStack.Pop();

                if (AutoExpanded)
                    ExpandBufferStack();

                if (_bufferStack.Count == 0)
                    throw new IndexOutOfRangeException("No enough available buffers.");

                return _bufferStack.Pop();
            }
        }

        public void ReturnBuffer(byte[] buffer)
        {
            if (buffer == null)
                throw new ArgumentNullException("buffer");

            lock (_sync)
            {
                _bufferStack.Push(buffer);
            }
        }

        public void ReturnBuffers(IEnumerable<byte[]> buffers)
        {
            if (buffers != null)
            {
                foreach (var buffer in buffers)
                {
                    ReturnBuffer(buffer);
                }
            }
        }

        public void ReturnBuffers(params byte[][] buffers)
        {
            if (buffers != null)
            {
                foreach (var buffer in buffers)
                {
                    ReturnBuffer(buffer);
                }
            }
        }
    }
}
