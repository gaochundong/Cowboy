namespace Cowboy.Sockets.Experimental
{
    public sealed class TcpSocketRioServerConfiguration
    {
        public TcpSocketRioServerConfiguration()
        {
            InitialPooledBufferCount = 100;
            ReceiveBufferSize = 8192;

            FrameBuilder = new LengthPrefixedFrameBuilder();
        }

        public int InitialPooledBufferCount { get; set; }
        public int ReceiveBufferSize { get; set; }

        public IFrameBuilder FrameBuilder { get; set; }
    }
}
