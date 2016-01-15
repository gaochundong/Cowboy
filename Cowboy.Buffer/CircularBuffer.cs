using System;
using System.Collections;
using System.Collections.Generic;

namespace Cowboy.Buffer
{
    // +-------------------+------------------+------------------+
    // | discard-able bytes|  readable bytes  |  writable bytes  |
    // |                   |     (CONTENT)    |                  |
    // +-------------------+------------------+------------------+
    // |                   |                  |                  |
    // 0      <=          head      <=       tail      <=    capacity
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

        public int Head { get { return _head; } }
        public int Tail { get { return _tail; } }
        public int Count { get { return _count; } }
        public int MaxCapacity { get { return _maxCapacity; } }

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
            _tail = _count;
            _count = newSize;
        }

        public void CopyFrom(T[] sourceArray, int sourceIndex, int length)
        {
            BufferValidator.ValidateBuffer(sourceArray, sourceIndex, length, "sourceArray", "sourceIndex", "length");

            if (Count + length >= Capacity)
                Capacity += length;

            if (Count + length > Capacity)
                throw new IndexOutOfRangeException(string.Format(
                    "No enough capacity to copy buffer, Capacity[{0}], MaxCapacity[{1}].", Capacity, MaxCapacity));

            var tail = _tail;
            if (tail + length < Capacity)
            {
                Array.Copy(sourceArray, sourceIndex, _buffer, tail, length);
                _tail = tail + length;
            }
            else
            {
                var firstCommitLength = Capacity - tail;
                var secondCommitLength = length - firstCommitLength;
                Array.Copy(sourceArray, sourceIndex, _buffer, tail, firstCommitLength);
                Array.Copy(sourceArray, sourceIndex + firstCommitLength, _buffer, 0, secondCommitLength);
                _tail = secondCommitLength;
            }
        }

        public void CopyTo(T[] destinationArray, int destinationIndex, int length)
        {
            BufferValidator.ValidateBuffer(destinationArray, destinationIndex, length, "destinationArray", "destinationIndex", "length");

            if (length > Count)
                length = Count;

            var head = _head;
            if (head + length < Capacity)
            {
                Array.Copy(_buffer, head, destinationArray, destinationIndex, length);
            }
            else
            {
                var firstCommitLength = Capacity - head;
                var secondCommitLength = length - firstCommitLength;
                Array.Copy(_buffer, head, destinationArray, destinationIndex, firstCommitLength);
                Array.Copy(_buffer, 0, destinationArray, destinationIndex + firstCommitLength, secondCommitLength);
            }
        }

        public void Discard()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public T[] ToArray()
        {
            var array = new T[Count];
            CopyTo(array, 0, Count);
            return array;
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
    }
}
