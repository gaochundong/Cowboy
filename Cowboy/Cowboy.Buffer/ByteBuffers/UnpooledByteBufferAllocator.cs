using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Buffer.ByteBuffers
{
    /// <summary>
    /// Unpooled implementation of <see cref="IByteBufferAllocator"/>.
    /// </summary>
    public class UnpooledByteBufferAllocator : AbstractByteBufferAllocator
    {
        public static readonly UnpooledByteBufferAllocator Default = new UnpooledByteBufferAllocator();

        protected override IByteBuffer NewBuffer(int initialCapacity, int maxCapacity)
        {
            return new UnpooledHeapByteBuffer(this, initialCapacity, maxCapacity);
        }
    }
}
