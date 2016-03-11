using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Cowboy.Buffer
{
    public class CircularBuffer<T> : IProducerConsumerCollection<T>, IEnumerable<T>, IEnumerable, ICollection
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

            ValidateBuffer(buffer, 0, initialCapacity, "buffer", null, "initialCapacity");

            _buffer = buffer;
            _currentCapacity = initialCapacity;
            _maxCapacity = maxCapacity;
        }

        public int MaxCapacity { get { return _maxCapacity; } }
        public int Capacity { get { return _currentCapacity; } set { AdjustCapacity(value); } }
        public int Head { get { return _head; } }
        public int Tail { get { return _tail; } }
        public int Count { get { return _count; } }
        public object SyncRoot { get { return _sync; } }
        public bool IsSynchronized { get { return false; } }

        private void AdjustCapacity(int newCapacity)
        {
            if (newCapacity <= 0)
                throw new ArgumentException("The new capacity must be greater than 0.", "newCapacity");

            if (newCapacity > _currentCapacity && _currentCapacity < MaxCapacity)
            {
                var adjustedCapacity = CalculateNewCapacity(newCapacity);
                ExpandBuffer(adjustedCapacity);
                _currentCapacity = adjustedCapacity;
            }
            else if (newCapacity < _currentCapacity)
            {
                ShrinkBuffer(newCapacity);
                _currentCapacity = newCapacity;
            }
        }

        private int CalculateNewCapacity(int requiredCapacity)
        {
            var newCapacity = 64;

            if (requiredCapacity > 1048576)
            {
                newCapacity = requiredCapacity;
                newCapacity += 1048576;
            }
            else
            {
                while (newCapacity < requiredCapacity)
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

        public void Add(T item)
        {
            if (_count + 1 > Capacity)
                Capacity += 1;

            _buffer[_tail % Capacity] = item;
            _tail++;

            if (_count < Capacity)
                _count++;
        }

        public void AddRange(IEnumerable<T> items)
        {
            if (_count + items.Count() > Capacity)
                Capacity += items.Count();

            foreach (var item in items)
            {
                _buffer[_tail % Capacity] = item;
                _tail++;

                if (_count < Capacity)
                    _count++;
            }
        }

        public bool TryAdd(T item)
        {
            if (Count == MaxCapacity)
                return false;

            Add(item);
            return true;
        }

        public T Take()
        {
            if (Count == 0)
                throw new InvalidOperationException("No item could be taken.");

            var item = _buffer[_head % Capacity];
            _head++;
            _count--;

            return item;
        }

        public IEnumerable<T> Take(int count)
        {
            if (count < 0)
                throw new ArgumentException("The count must be greater than 0.", "count");
            if (Count == 0)
                throw new InvalidOperationException("No enough items could be taken.");

            var items = new List<T>(Math.Min(count, Count));
            for (var i = 0; i < items.Capacity; i++, _head++, _count--)
            {
                items.Add(_buffer[_head % Capacity]);
            }

            return items;
        }

        public IEnumerable<T> TakeAll()
        {
            return Take(Count);
        }

        public bool TryTake(out T item)
        {
            item = default(T);

            if (Count > 0)
            {
                item = _buffer[_head % Capacity];
                _head++;
                _count--;

                return true;
            }

            return false;
        }

        public void Skip(int count)
        {
            if (_count >= count)
            {
                _head += count;
                _count -= count;
            }
        }

        public T Peek()
        {
            if (Count == 0)
                throw new InvalidOperationException("No item could be peeked.");

            return _buffer[_head % Capacity];
        }

        public bool TryPeek(out T item)
        {
            item = default(T);

            if (Count > 0)
            {
                item = _buffer[_head % Capacity];
                return true;
            }

            return false;
        }

        public void Clear()
        {
            _head = 0;
            _tail = 0;
            _count = 0;
        }

        public T[] ToArray()
        {
            var newArray = new T[Count];
            CopyTo(newArray, 0, newArray.Length);
            return newArray;
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
            private int _internalIndex = -1;

            public CircularBufferIterator(CircularBuffer<T> container)
            {
                _container = container;
            }

            public T Current
            {
                get
                {
                    return _container[(_container.Head + _internalIndex) % _container.Capacity];
                }
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            public bool MoveNext()
            {
                _internalIndex++;

                if (_internalIndex >= _container.Count)
                    return false;
                else
                    return true;
            }

            public void Reset()
            {
                _internalIndex = 0;
            }

            public void Dispose()
            {
                _internalIndex = 0;
            }
        }

        #endregion

        #region Copy

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
            ValidateBuffer(sourceArray, sourceIndex, length, "sourceArray", "sourceIndex", "length");

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

        public void CopyFrom(CircularBuffer<T> sourceBuffer)
        {
            if (sourceBuffer == null)
                throw new ArgumentNullException("sourceBuffer");
            CopyFrom(sourceBuffer, 0, sourceBuffer.Count);
        }

        public void CopyFrom(CircularBuffer<T> sourceBuffer, int sourceOffset)
        {
            CopyFrom(sourceBuffer, sourceOffset, sourceBuffer.Count - sourceOffset);
        }

        public void CopyFrom(CircularBuffer<T> sourceBuffer, int sourceOffset, int count)
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
                            var first = right;
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

        public void CopyTo(Array destinationArray)
        {
            CopyTo(destinationArray, 0, Count);
        }

        public void CopyTo(Array destinationArray, int destinationIndex)
        {
            CopyTo(destinationArray, destinationIndex, Count);
        }

        public void CopyTo(Array destinationArray, int destinationIndex, int length)
        {
            ValidateBuffer(destinationArray, destinationIndex, length, "destinationArray", "destinationIndex", "length");

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

        public void CopyTo(T[] destinationArray)
        {
            CopyTo(destinationArray, 0, Count);
        }

        public void CopyTo(T[] destinationArray, int destinationIndex)
        {
            CopyTo(destinationArray, destinationIndex, Count);
        }

        public void CopyTo(T[] destinationArray, int destinationIndex, int length)
        {
            Array array = destinationArray;
            CopyTo(array, destinationIndex, length);
        }

        public void CopyTo(CircularBuffer<T> destinationBuffer)
        {
            if (destinationBuffer == null)
                throw new ArgumentNullException("destinationBuffer");
            CopyTo(destinationBuffer, 0, Count);
        }

        public void CopyTo(CircularBuffer<T> destinationBuffer, int count)
        {
            CopyTo(destinationBuffer, 0, count);
        }

        public void CopyTo(CircularBuffer<T> destinationBuffer, int sourceOffset, int count)
        {
            if (destinationBuffer == null)
                throw new ArgumentNullException("destinationBuffer");
            destinationBuffer.CopyFrom(this, sourceOffset, count);
        }

        #endregion

        private static void ValidateBuffer<B>(B[] buffer, int offset, int count,
            string bufferParameterName = null,
            string offsetParameterName = null,
            string countParameterName = null)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(!string.IsNullOrEmpty(bufferParameterName) ? bufferParameterName : "buffer");
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(!string.IsNullOrEmpty(offsetParameterName) ? offsetParameterName : "offset");
            }

            if (count < 0 || count > (buffer.Length - offset))
            {
                throw new ArgumentOutOfRangeException(!string.IsNullOrEmpty(countParameterName) ? countParameterName : "count");
            }
        }

        private static void ValidateBuffer(Array buffer, int offset, int count,
            string bufferParameterName = null,
            string offsetParameterName = null,
            string countParameterName = null)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(!string.IsNullOrEmpty(bufferParameterName) ? bufferParameterName : "buffer");
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException(!string.IsNullOrEmpty(offsetParameterName) ? offsetParameterName : "offset");
            }

            if (count < 0 || count > (buffer.Length - offset))
            {
                throw new ArgumentOutOfRangeException(!string.IsNullOrEmpty(countParameterName) ? countParameterName : "count");
            }
        }
    }
}
