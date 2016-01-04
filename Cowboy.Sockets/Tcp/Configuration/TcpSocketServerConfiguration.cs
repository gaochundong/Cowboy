using System;
using System.Net.Sockets;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketServerConfiguration
    {
        public TcpSocketServerConfiguration()
        {
            Framing = true;
            InitialBufferAllocationCount = 100;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            ExclusiveAddressUse = true;
            NoDelay = true;
            LingerState = new LingerOption(false, 0);

            PendingConnectionBacklog = 200;
            AllowNatTraversal = true;
        }

        public bool Framing { get; set; }
        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool ExclusiveAddressUse { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }

        public int PendingConnectionBacklog { get; set; }
        public bool AllowNatTraversal { get; set; }        
    }
}
