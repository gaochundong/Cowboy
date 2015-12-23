namespace Cowboy.Sockets
{
    public sealed class TcpSocketServerConfiguration
    {
        public TcpSocketServerConfiguration()
        {
            InitialBufferAllocationCount = 100;
            ReceiveBufferSize = 64;
            PendingConnectionBacklog = 200;
            IsPackingEnabled = true;
            AllowNatTraversal = true;
            ExclusiveAddressUse = true;
        }

        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int PendingConnectionBacklog { get; set; }
        public bool IsPackingEnabled { get; set; }
        public bool AllowNatTraversal { get; set; }
        public bool ExclusiveAddressUse { get; set; }
    }
}
