using System.Collections.Generic;

namespace Cowboy.Buffer
{
    public interface IBufferManager
    {
        byte[] BorrowBuffer();
        IEnumerable<byte[]> BorrowBuffers(int count);
        void ReturnBuffer(byte[] buffer);
        void ReturnBuffers(IEnumerable<byte[]> buffers);
        void ReturnBuffers(params byte[][] buffers);
    }
}
