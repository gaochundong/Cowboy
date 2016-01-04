using System;
using System.Net.Sockets;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketClientConfiguration
    {
        public TcpSocketClientConfiguration()
        {
            IsFramingEnabled = true;
            InitialBufferAllocationCount = 4;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            ExclusiveAddressUse = true;
            NoDelay = true;
            LingerState = new LingerOption(false, 0);
        }

        public bool IsFramingEnabled { get; set; }
        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool ExclusiveAddressUse { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }
    }
}
