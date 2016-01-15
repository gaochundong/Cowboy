using System;

namespace Cowboy.Buffer
{
    public class CircularBuffer<T>
    {
        private readonly object _sync = new object();
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

        public int Count
        {
            get
            {
                lock (_sync)
                {
                    return _count;
                }
            }
        }

        public int MaxCapacity
        {
            get
            {
                lock (_sync)
                {
                    return _maxCapacity;
                }
            }
        }

        public int Capacity
        {
            get
            {
                lock (_sync)
                {
                    return _currentCapacity;
                }
            }
            set
            {
                lock (_sync)
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

            lock (_sync)
            {
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
        }

        public void CopyTo(T[] destinationArray, int destinationIndex, int length)
        {
            BufferValidator.ValidateBuffer(destinationArray, destinationIndex, length, "destinationArray", "destinationIndex", "length");

            lock (_sync)
            {
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
        }

        public T[] ToArray()
        {
            lock (_sync)
            {
                var array = new T[Count];
                CopyTo(array, 0, Count);
                return array;
            }
        }
    }
}
