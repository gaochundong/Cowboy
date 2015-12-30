using System;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketServerConfiguration
    {
        public TcpSocketServerConfiguration()
        {
            IsPackingEnabled = true;
            InitialBufferAllocationCount = 100;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            ExclusiveAddressUse = true;
            NoDelay = true;

            PendingConnectionBacklog = 200;
            AllowNatTraversal = true;
            ExclusiveAddressUse = true;
        }

        public bool IsPackingEnabled { get; set; }
        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool NoDelay { get; set; }

        public int PendingConnectionBacklog { get; set; }
        public bool AllowNatTraversal { get; set; }
        public bool ExclusiveAddressUse { get; set; }
    }
}
