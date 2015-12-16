using System.Collections.Generic;

namespace Cowboy.WebSockets
{
    public interface IBufferManager
    {
        byte[] BorrowBuffer();
        void ReturnBuffer(byte[] buffer);
        void ReturnBuffers(IEnumerable<byte[]> buffers);
    }
}
