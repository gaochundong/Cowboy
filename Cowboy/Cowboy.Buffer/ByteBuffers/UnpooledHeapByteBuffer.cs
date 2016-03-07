using System;
using System.Collections.Generic;
using System.Diagnostics.Contracts;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Buffer.ByteBuffers
{
    public class UnpooledHeapByteBuffer : AbstractReferenceCountedByteBuffer
    {
        private readonly IByteBufferAllocator _allocator;
        private byte[] _array;

        public UnpooledHeapByteBuffer(IByteBufferAllocator allocator, int initialCapacity, int maxCapacity)
            : this(allocator, new byte[initialCapacity], 0, 0, maxCapacity)
        {
        }

        public UnpooledHeapByteBuffer(IByteBufferAllocator allocator, byte[] initialArray, int maxCapacity)
            : this(allocator, initialArray, 0, initialArray.Length, maxCapacity)
        {
        }

        public UnpooledHeapByteBuffer(
            IByteBufferAllocator allocator, byte[] initialArray, int readerIndex, int writerIndex, int maxCapacity)
            : base(maxCapacity)
        {
            Contract.Requires(allocator != null);
            Contract.Requires(initialArray != null);
            Contract.Requires(initialArray.Length <= maxCapacity);

            _allocator = allocator;
            this.SetArray(initialArray);
            this.SetIndex(readerIndex, writerIndex);
        }

        protected void SetArray(byte[] initialArray)
        {
            _array = initialArray;
        }

        public override IByteBufferAllocator Allocator
        {
            get { return _allocator; }
        }

        public override ByteOrder Order
        {
            get { return ByteOrder.BigEndian; }
        }

        public override int Capacity
        {
            get
            {
                this.EnsureAccessible();
                return _array.Length;
            }
        }

        public override IByteBuffer AdjustCapacity(int newCapacity)
        {
            this.EnsureAccessible();
            Contract.Requires(newCapacity >= 0 && newCapacity <= this.MaxCapacity);

            int oldCapacity = _array.Length;
            if (newCapacity > oldCapacity)
            {
                var newArray = new byte[newCapacity];
                System.Array.Copy(_array, 0, newArray, 0, _array.Length);
                this.SetArray(newArray);
            }
            else if (newCapacity < oldCapacity)
            {
                var newArray = new byte[newCapacity];
                int readerIndex = this.ReaderIndex;
                if (readerIndex < newCapacity)
                {
                    int writerIndex = this.WriterIndex;
                    if (writerIndex > newCapacity)
                    {
                        this.SetWriterIndex(writerIndex = newCapacity);
                    }
                    System.Array.Copy(_array, readerIndex, newArray, readerIndex, writerIndex - readerIndex);
                }
                else
                {
                    this.SetIndex(newCapacity, newCapacity);
                }
                this.SetArray(newArray);
            }
            return this;
        }

        //public override bool HasArray
        //{
        //    get { return true; }
        //}

        //public override byte[] Array
        //{
        //    get
        //    {
        //        this.EnsureAccessible();
        //        return _array;
        //    }
        //}

        //public override int ArrayOffset
        //{
        //    get { return 0; }
        //}

        public override IByteBuffer GetBytes(int index, IByteBuffer dst, int dstIndex, int length)
        {
            //this.CheckDstIndex(index, length, dstIndex, dst.Capacity);
            //if (dst.HasArray)
            //{
            //    this.GetBytes(index, dst.Array, dst.ArrayOffset + dstIndex, length);
            //}
            //else
            //{
            //    dst.SetBytes(dstIndex, _array, index, length);
            //}
            //return this;
            return null;
        }

        public override IByteBuffer GetBytes(int index, byte[] dst, int dstIndex, int length)
        {
            this.CheckDstIndex(index, length, dstIndex, dst.Length);
            System.Array.Copy(_array, index, dst, dstIndex, length);
            return this;
        }

        public override IByteBuffer GetBytes(int index, Stream destination, int length)
        {
            //destination.Write(this.Array, this.ArrayOffset + this.ReaderIndex, this.ReadableBytes);
            //return this;
            return null;
        }

        public override IByteBuffer SetBytes(int index, IByteBuffer src, int srcIndex, int length)
        {
            //this.CheckSrcIndex(index, length, srcIndex, src.Capacity);
            //if (src.HasArray)
            //{
            //    this.SetBytes(index, src.Array, src.ArrayOffset + srcIndex, length);
            //}
            //else
            //{
            //    src.GetBytes(srcIndex, _array, index, length);
            //}
            //return this;
            return null;
        }

        public override IByteBuffer SetBytes(int index, byte[] src, int srcIndex, int length)
        {
            this.CheckSrcIndex(index, length, srcIndex, src.Length);
            System.Array.Copy(src, srcIndex, _array, index, length);
            return this;
        }
        public override async Task<int> SetBytesAsync(int index, Stream src, int length, CancellationToken cancellationToken)
        {
            //int readTotal = 0;
            //int read;
            //int offset = this.ArrayOffset + index;
            //do
            //{
            //    read = await src.ReadAsync(this.Array, offset + readTotal, length - readTotal, cancellationToken);
            //    readTotal += read;
            //}
            //while (read > 0 && readTotal < length);

            //return readTotal;
            await Task.CompletedTask;
            return 0;
        }

        public override byte GetByte(int index)
        {
            this.EnsureAccessible();
            return this._GetByte(index);
        }

        protected override byte _GetByte(int index)
        {
            return _array[index];
        }

        public override short GetShort(int index)
        {
            this.EnsureAccessible();
            return this._GetShort(index);
        }

        protected override short _GetShort(int index)
        {
            return unchecked((short)(_array[index] << 8 | _array[index + 1]));
        }

        public override int GetInt(int index)
        {
            this.EnsureAccessible();
            return this._GetInt(index);
        }

        protected override int _GetInt(int index)
        {
            return unchecked(_array[index] << 24 |
                _array[index + 1] << 16 |
                _array[index + 2] << 8 |
                _array[index + 3]);
        }

        public override long GetLong(int index)
        {
            this.EnsureAccessible();
            return this._GetLong(index);
        }

        protected override long _GetLong(int index)
        {
            unchecked
            {
                int i1 = _array[index] << 24 |
                    _array[index + 1] << 16 |
                    _array[index + 2] << 8 |
                    _array[index + 3];
                int i2 = _array[index + 4] << 24 |
                    _array[index + 5] << 16 |
                    _array[index + 6] << 8 |
                    _array[index + 7];
                return (uint)i2 | ((long)i1 << 32);
            }
        }

        public override IByteBuffer SetByte(int index, int value)
        {
            this.EnsureAccessible();
            this._SetByte(index, value);
            return this;
        }

        protected override void _SetByte(int index, int value)
        {
            _array[index] = (byte)value;
        }

        public override IByteBuffer SetShort(int index, int value)
        {
            this.EnsureAccessible();
            this._SetShort(index, value);
            return this;
        }

        protected override void _SetShort(int index, int value)
        {
            unchecked
            {
                _array[index] = (byte)((ushort)value >> 8);
                _array[index + 1] = (byte)value;
            }
        }

        public override IByteBuffer SetInt(int index, int value)
        {
            this.EnsureAccessible();
            this._SetInt(index, value);
            return this;
        }

        protected override void _SetInt(int index, int value)
        {
            unchecked
            {
                uint unsignedValue = (uint)value;
                _array[index] = (byte)(unsignedValue >> 24);
                _array[index + 1] = (byte)(unsignedValue >> 16);
                _array[index + 2] = (byte)(unsignedValue >> 8);
                _array[index + 3] = (byte)value;
            }
        }

        public override IByteBuffer SetLong(int index, long value)
        {
            this.EnsureAccessible();
            this._SetLong(index, value);
            return this;
        }

        protected override void _SetLong(int index, long value)
        {
            unchecked
            {
                ulong unsignedValue = (ulong)value;
                _array[index] = (byte)(unsignedValue >> 56);
                _array[index + 1] = (byte)(unsignedValue >> 48);
                _array[index + 2] = (byte)(unsignedValue >> 40);
                _array[index + 3] = (byte)(unsignedValue >> 32);
                _array[index + 4] = (byte)(unsignedValue >> 24);
                _array[index + 5] = (byte)(unsignedValue >> 16);
                _array[index + 6] = (byte)(unsignedValue >> 8);
                _array[index + 7] = (byte)value;
            }
        }

        public override IByteBuffer Copy(int index, int length)
        {
            this.CheckIndex(index, length);
            var copiedArray = new byte[length];
            System.Array.Copy(_array, index, copiedArray, 0, length);
            return new UnpooledHeapByteBuffer(this.Allocator, copiedArray, this.MaxCapacity);
        }

        protected override void Deallocate()
        {
            _array = null;
        }

        public override IByteBuffer Unwrap()
        {
            return null;
        }
    }
}
