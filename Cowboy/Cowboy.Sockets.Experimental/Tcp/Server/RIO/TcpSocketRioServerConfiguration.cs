using Cowboy.Buffer;

namespace Cowboy.Sockets.Experimental
{
    public sealed class TcpSocketRioServerConfiguration
    {
        public TcpSocketRioServerConfiguration()
            : this(new SegmentBufferManager(1024, 8192, 1, true))
        {
        }

        public TcpSocketRioServerConfiguration(ISegmentBufferManager bufferManager)
        {
            BufferManager = bufferManager;

            ReceiveBufferSize = 8192;

            FrameBuilder = new LengthPrefixedFrameBuilder();
        }

        public ISegmentBufferManager BufferManager { get; set; }

        public int ReceiveBufferSize { get; set; }

        public IFrameBuilder FrameBuilder { get; set; }
    }
}
