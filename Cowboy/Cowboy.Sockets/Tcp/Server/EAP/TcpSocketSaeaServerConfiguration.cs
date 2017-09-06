using System;
using System.Net.Sockets;
using Cowboy.Buffer;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketSaeaServerConfiguration
    {
        public TcpSocketSaeaServerConfiguration()
            : this(new SegmentBufferManager(1024, 8192, 1, true))
        {
        }

        public TcpSocketSaeaServerConfiguration(ISegmentBufferManager bufferManager)
        {
            BufferManager = bufferManager;

            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            NoDelay = true;
            LingerState = new LingerOption(false, 0);
            KeepAlive = false;
            KeepAliveInterval = TimeSpan.FromSeconds(5);
            ReuseAddress = false;

            PendingConnectionBacklog = 200;
            AllowNatTraversal = true;

            FrameBuilder = new LengthPrefixedFrameBuilder();
        }

        public ISegmentBufferManager BufferManager { get; set; }

        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }
        public bool KeepAlive { get; set; }
        public TimeSpan KeepAliveInterval { get; set; }
        public bool ReuseAddress { get; set; }

        public int PendingConnectionBacklog { get; set; }
        public bool AllowNatTraversal { get; set; }

        public IFrameBuilder FrameBuilder { get; set; }
    }
}
