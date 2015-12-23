namespace Cowboy.Sockets
{
    public sealed class TcpSocketClientConfiguration
    {
        public TcpSocketClientConfiguration()
        {
            InitialBufferAllocationCount = 4;
            ReceiveBufferSize = 8192;
            IsPackingEnabled = true;
        }

        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public bool IsPackingEnabled { get; set; }
    }
}
