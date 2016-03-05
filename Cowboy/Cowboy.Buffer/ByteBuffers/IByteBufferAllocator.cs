using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Buffer.ByteBuffers
{
    /// <summary>
    /// Thread-safe interface for allocating <see cref="IByteBuffer"/> instances
    /// </summary>
    public interface IByteBufferAllocator
    {
        IByteBuffer Buffer();

        IByteBuffer Buffer(int initialCapacity);

        IByteBuffer Buffer(int initialCapacity, int maxCapacity);

        //CompositeByteBuffer CompositeBuffer();

        //CompositeByteBuffer CompositeBuffer(int maxComponents);
    }
}
