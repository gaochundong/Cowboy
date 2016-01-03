using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Sockets;

namespace Cowboy.TcpLika
{
    internal class TcpLikaEngine : IAsyncTcpSocketClientMessageDispatcher
    {
        private TcpLikaCommandLineOptions _options;

        public TcpLikaEngine(TcpLikaCommandLineOptions options)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            _options = options;
        }

        public void Start()
        {
            var config = GetClientConfiguration();
            var remoteEP = _options.RemoteEndPoints.First();

            var client = new AsyncTcpSocketClient(remoteEP, this, config);
            client.Connect();
        }

        private AsyncTcpSocketClientConfiguration GetClientConfiguration()
        {
            var config = new AsyncTcpSocketClientConfiguration();

            if (_options.IsSetNagle)
                config.NoDelay = _options.Nagle;

            if (_options.IsSetReceiveBufferSize)
                config.ReceiveBufferSize = _options.ReceiveBufferSize;

            if (_options.IsSetSendBufferSize)
                config.SendBufferSize = _options.SendBufferSize;

            return config;
        }

        public async Task Dispatch(AsyncTcpSocketClient client, byte[] data, int offset, int count)
        {
            await Task.Yield();
        }
    }
}
