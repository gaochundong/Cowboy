using Cowboy.Buffer;

namespace Cowboy.Sockets.Experimental
{
    public sealed class TcpSocketRioServerConfiguration
    {
        public TcpSocketRioServerConfiguration()
        {
            BufferManager = new GrowingByteBufferManager(20, 8192);
            ReceiveBufferSize = 8192;

            FrameBuilder = new LengthPrefixedFrameBuilder();
        }

        public IBufferManager BufferManager { get; set; }
        public int ReceiveBufferSize { get; set; }

        public IFrameBuilder FrameBuilder { get; set; }
    }
}
