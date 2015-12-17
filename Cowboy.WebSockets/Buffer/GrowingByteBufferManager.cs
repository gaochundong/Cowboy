using System;
using System.Collections.Generic;

namespace Cowboy.WebSockets
{
    public class GrowingByteBufferManager : IBufferManager
    {
        public int _bufferCount;
        public int _bufferSize;
        private Stack<byte[]> _bufferStack;
        private readonly object _sync = new object();

        public GrowingByteBufferManager(int initBufferCount, int initBufferSize)
        {
            _bufferCount = initBufferCount;
            _bufferSize = initBufferSize;

            AutoExpand = true;
            AutoExpandScale = 1.5;

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

        public bool AutoExpand { get; set; }

        public double AutoExpandScale { get; set; }

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
            if (AutoExpandScale < 1)
                throw new ArgumentException("Invalid auto expanded scale, the value should not be less than 1.");

            int currentBufferCount = _bufferCount;
            _bufferCount = (int)(_bufferCount * AutoExpandScale);

            for (int i = 0; i < _bufferCount - currentBufferCount; i++)
            {
                byte[] buffer = new byte[_bufferSize];
                _bufferStack.Push(buffer);
            }
        }

        public byte[] BorrowBuffer()
        {
            lock (_sync)
            {
                if (_bufferStack.Count > 0)
                    return _bufferStack.Pop();

                if (AutoExpand)
                    ExpandBufferStack();

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
