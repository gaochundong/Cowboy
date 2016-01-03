using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            var config = BuildClientConfiguration();
            var remoteEP = _options.RemoteEndPoints.First();

            int threads = 1;
            int connections = 1;
            int connectionsPerThread = 1;

            if (_options.IsSetThreads)
                threads = _options.Threads;
            if (threads > Environment.ProcessorCount)
                threads = Environment.ProcessorCount;

            if (_options.IsSetConnections)
                connections = _options.Connections;

            if (connections < threads)
                threads = connections;

            connectionsPerThread = connections / threads;

            var tasks = new List<Task>();
            for (int p = 0; p < threads; p++)
            {
                var task = Task.Run(async () =>
                {
                    await Load(config, remoteEP, connectionsPerThread);
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
        }

        private async Task Load(AsyncTcpSocketClientConfiguration config, IPEndPoint remoteEP, int connections)
        {
            var channels = new List<AsyncTcpSocketClient>();

            for (int i = 0; i < connections; i++)
            {
                var client = new AsyncTcpSocketClient(remoteEP, this, config);
                if (!_options.IsSetConnectTimeout)
                {
                    await client.ConnectAsync();
                }
                else
                {
                    var task = client.ConnectAsync();
                    if (!task.Wait(_options.ConnectTimeout))
                    {
                        client.Close();
                    }
                }
                channels.Add(client);
            }

            if (_options.IsSetChannelLifetime)
                await Task.Delay(_options.ChannelLifetime);

            foreach (var client in channels)
            {
                client.Close();
            }
        }

        private AsyncTcpSocketClientConfiguration BuildClientConfiguration()
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
            await Task.CompletedTask;
        }
    }
}
