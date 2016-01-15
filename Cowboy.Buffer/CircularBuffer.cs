using System;
using System.Collections;
using System.Collections.Generic;

namespace Cowboy.Buffer
{
    public class CircularBuffer<T> : IEnumerable<T>, IEnumerable
    {
        private T[] _buffer;
        private int _currentCapacity;
        private int _maxCapacity;

        private int _head = 0;
        private int _tail = 0;
        private int _count = 0;

        public CircularBuffer(int initialCapacity)
            : this(initialCapacity, int.MaxValue)
        {
        }

        public CircularBuffer(int initialCapacity, int maxCapacity)
            : this(new T[initialCapacity], initialCapacity, maxCapacity)
        {
        }

        public CircularBuffer(T[] buffer)
            : this(buffer, buffer.Length, int.MaxValue)
        {
        }

        public CircularBuffer(T[] buffer, int initialCapacity)
            : this(buffer, initialCapacity, int.MaxValue)
        {
        }

        public CircularBuffer(T[] buffer, int initialCapacity, int maxCapacity)
        {
            if (initialCapacity <= 0)
                throw new ArgumentException("The initial capacity must be greater than 0.", "initialCapacity");
            if (maxCapacity <= 0)
                throw new ArgumentException("The max capacity must be greater than 0.", "maxCapacity");
            if (initialCapacity > maxCapacity)
                throw new ArgumentException("The max capacity must be greater than initial capacity.", "maxCapacity");

            BufferValidator.ValidateBuffer(buffer, 0, initialCapacity, "buffer", null, "initialCapacity");

            _buffer = buffer;
            _currentCapacity = initialCapacity;
            _maxCapacity = maxCapacity;
        }

        public int MaxCapacity { get { return _maxCapacity; } }
        public int Head { get { return _head; } }
        public int Tail { get { return _tail; } }
        public int Count { get { return _count; } }

        public int Capacity
        {
            get
            {
                return _currentCapacity;
            }
            set
            {
                if (value > _currentCapacity && _currentCapacity < MaxCapacity)
                {
                    var newCapacity = CalculateNewCapacity(value);
                    ExpandBuffer(newCapacity);
                    _currentCapacity = newCapacity;
                }
                else if (value < _currentCapacity)
                {
                    ShrinkBuffer(value);
                    _currentCapacity = value;
                }
            }
        }

        private int CalculateNewCapacity(int minNewCapacity)
        {
            var newCapacity = 64;

            if (minNewCapacity > 1048576)
            {
                newCapacity = minNewCapacity;
                newCapacity += 1048576;
            }
            else
            {
                while (newCapacity < minNewCapacity)
                {
                    newCapacity <<= 1;
                }
            }

            return Math.Min(newCapacity, MaxCapacity);
        }

        private void ExpandBuffer(int newSize)
        {
            var newBuffer = new T[newSize];

            CopyTo(newBuffer, 0, _count);

            _buffer = newBuffer;
            _head = 0;
            _tail = _count;
        }

        private void ShrinkBuffer(int newSize)
        {
            var newBuffer = new T[newSize];

            CopyTo(newBuffer, 0, newSize);

            _buffer = newBuffer;
            _head = 0;
            _tail = _count >= newSize ? newSize : _count;
            _count = _count >= newSize ? newSize : _count;
        }

        public void CopyFrom(T[] sourceArray)
        {
            if (sourceArray == null)
                throw new ArgumentNullException("sourceArray");
            CopyFrom(sourceArray, 0, sourceArray.Length);
        }

        public void CopyFrom(T[] sourceArray, int length)
        {
            CopyFrom(sourceArray, 0, length);
        }

        public void CopyFrom(T[] sourceArray, int sourceIndex, int length)
        {
            BufferValidator.ValidateBuffer(sourceArray, sourceIndex, length, "sourceArray", "sourceIndex", "length");

            if (Count + length >= Capacity)
                Capacity += length;

            if (Count + length > Capacity)
                throw new IndexOutOfRangeException(string.Format(
                    "No enough capacity to copy buffer, Capacity[{0}], MaxCapacity[{1}].", Capacity, MaxCapacity));

            if (_tail - 1 + length < Capacity)
            {
                Array.Copy(sourceArray, sourceIndex, _buffer, _tail, length);
                _tail += length;
                _count += length;
            }
            else
            {
                var firstCommitLength = Capacity - _tail;
                var secondCommitLength = length - firstCommitLength;
                Array.Copy(sourceArray, sourceIndex, _buffer, _tail, firstCommitLength);
                Array.Copy(sourceArray, sourceIndex + firstCommitLength, _buffer, 0, secondCommitLength);
                _tail = secondCommitLength;
                _count += length;
            }
        }

        public void AppendFrom(CircularBuffer<T> sourceBuffer)
        {
            if (sourceBuffer == null)
                throw new ArgumentNullException("sourceBuffer");
            AppendFrom(sourceBuffer, 0, sourceBuffer.Count);
        }

        public void AppendFrom(CircularBuffer<T> sourceBuffer, int count)
        {
            AppendFrom(sourceBuffer, 0, count);
        }

        public void AppendFrom(CircularBuffer<T> sourceBuffer, int sourceOffset, int count)
        {
            if (sourceBuffer == null)
                throw new ArgumentNullException("sourceBuffer");
            if (sourceOffset < 0 || sourceOffset > sourceBuffer.Count)
                throw new ArgumentOutOfRangeException("sourceOffset");
            if (count < 0 || count > (sourceBuffer.Count - sourceOffset))
                throw new ArgumentOutOfRangeException("count");

            if (Count + count >= Capacity)
                Capacity += count;

            if (Count + count > Capacity)
                throw new IndexOutOfRangeException(string.Format(
                    "No enough capacity to copy buffer, Capacity[{0}], MaxCapacity[{1}].", Capacity, MaxCapacity));

            if (_tail + count <= Capacity)
            {
                if (sourceBuffer.Head + sourceOffset < sourceBuffer.Capacity)
                {
                    if (sourceBuffer.Head + sourceOffset + count < sourceBuffer.Capacity)
                    {
                        Array.Copy(sourceBuffer._buffer, sourceBuffer.Head + sourceOffset, _buffer, _tail, count);
                    }
                    else
                    {
                        var first = sourceBuffer.Capacity - (sourceBuffer.Head + sourceOffset);
                        var second = count - first;
                        Array.Copy(sourceBuffer._buffer, sourceBuffer.Head + sourceOffset, _buffer, _tail, first);
                        Array.Copy(sourceBuffer._buffer, 0, _buffer, _tail + first, second);
                    }
                }
                else
                {
                    var index = (sourceBuffer.Head + sourceOffset) % sourceBuffer.Capacity;
                    Array.Copy(sourceBuffer._buffer, index, _buffer, _tail, count);
                }

                _tail += count;
                _count += count;
            }
            else
            {
                var right = Capacity - _tail;

                if (sourceBuffer.Head + sourceOffset < sourceBuffer.Capacity)
                {
                    if (sourceBuffer.Head + sourceOffset + count < sourceBuffer.Capacity)
                    {
                        var first = right;
                        var second = count - first;
                        Array.Copy(sourceBuffer._buffer, sourceBuffer.Head + sourceOffset, _buffer, _tail, first);
                        Array.Copy(sourceBuffer._buffer, sourceBuffer.Head + sourceOffset + first, _buffer, 0, second);
                    }
                    else
                    {
                        var part1 = sourceBuffer.Capacity - (sourceBuffer.Head + sourceOffset);
                        var part2 = count - part1;
                        if (part1 < right)
                        {
                            var first = part1;
                            var second = right - part1;
                            var third = count - second;
                            Array.Copy(sourceBuffer._buffer, sourceBuffer.Head + sourceOffset, _buffer, _tail, first);
                            Array.Copy(sourceBuffer._buffer, 0, _buffer, _tail + first, second);
                            Array.Copy(sourceBuffer._buffer, second, _buffer, 0, third);
                        }
                        else
                        {
                            var first = part1 - right;
                            var second = part1 - first;
                            var third = part2;
                            Array.Copy(sourceBuffer._buffer, sourceBuffer.Head + sourceOffset, _buffer, _tail, first);
                            Array.Copy(sourceBuffer._buffer, sourceBuffer.Head + sourceOffset + first, _buffer, 0, second);
                            Array.Copy(sourceBuffer._buffer, 0, _buffer, second, third);
                        }
                    }
                }
                else
                {
                    var index = (sourceBuffer.Head + sourceOffset) % sourceBuffer.Capacity;
                    var first = right;
                    var second = count - first;
                    Array.Copy(sourceBuffer._buffer, index, _buffer, _tail, first);
                    Array.Copy(sourceBuffer._buffer, index + first, _buffer, 0, second);
                }

                _tail = count - right;
                _count += count;
            }
        }

        public void CopyTo(T[] destinationArray)
        {
            if (destinationArray == null)
                throw new ArgumentNullException("destinationArray");
            CopyTo(destinationArray, 0, destinationArray.Length);
        }

        public void CopyTo(T[] destinationArray, int length)
        {
            CopyTo(destinationArray, 0, length);
        }

        public void CopyTo(T[] destinationArray, int destinationIndex, int length)
        {
            BufferValidator.ValidateBuffer(destinationArray, destinationIndex, length, "destinationArray", "destinationIndex", "length");

            if (length > Count)
                length = Count;

            if (_head + length < Capacity)
            {
                Array.Copy(_buffer, _head, destinationArray, destinationIndex, length);
            }
            else
            {
                var firstCommitLength = Capacity - _head;
                var secondCommitLength = length - firstCommitLength;
                Array.Copy(_buffer, _head, destinationArray, destinationIndex, firstCommitLength);
                Array.Copy(_buffer, 0, destinationArray, destinationIndex + firstCommitLength, secondCommitLength);
            }
        }

        public void AppendTo(CircularBuffer<T> destinationBuffer)
        {
            if (destinationBuffer == null)
                throw new ArgumentNullException("destinationBuffer");
            AppendTo(destinationBuffer, 0, Count);
        }

        public void AppendTo(CircularBuffer<T> destinationBuffer, int count)
        {
            AppendTo(destinationBuffer, 0, count);
        }

        public void AppendTo(CircularBuffer<T> destinationBuffer, int sourceOffset, int count)
        {
            if (destinationBuffer == null)
                throw new ArgumentNullException("destinationBuffer");
            destinationBuffer.AppendFrom(this, sourceOffset, count);
        }

        public void Discard()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public T this[int index]
        {
            get
            {
                return _buffer[(_head + index) % Capacity];
            }
            set
            {
                _buffer[(_head + index) % Capacity] = value;
            }
        }

        #region IEnumerable

        public IEnumerator<T> GetEnumerator()
        {
            return new CircularBufferIterator(this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private class CircularBufferIterator : IEnumerator<T>
        {
            private CircularBuffer<T> _container;
            private int _index = -1;

            public CircularBufferIterator(CircularBuffer<T> container)
            {
                _container = container;
            }

            public T Current
            {
                get
                {
                    return _container[(_container.Head + _index) % _container.Capacity];
                }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                _index++;

                if (_index >= _container.Count)
                    return false;
                else
                    return true;
            }

            public void Reset()
            {
                _index = 0;
            }

            public void Dispose()
            {
                _index = 0;
            }
        }

        #endregion
    }
}
