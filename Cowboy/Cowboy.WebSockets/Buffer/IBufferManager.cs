using System.Collections.Generic;

namespace Cowboy.WebSockets.Buffer
{
    public interface IBufferManager
    {
        byte[] BorrowBuffer();
        void ReturnBuffer(byte[] buffer);
        void ReturnBuffers(IEnumerable<byte[]> buffers);
        void ReturnBuffers(params byte[][] buffers);
    }
}
