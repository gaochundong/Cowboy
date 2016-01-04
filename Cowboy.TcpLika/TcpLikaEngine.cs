using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
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
                    await Load(connectionsPerThread);
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
        }

        private async Task Load(int connections)
        {
            var config = BuildClientConfiguration();
            var remoteEP = _options.RemoteEndPoints.First();

            var channels = new List<AsyncTcpSocketClient>();

            for (int c = 0; c < connections; c++)
            {
                try
                {
                    var client = new AsyncTcpSocketClient(remoteEP, this, config);
                    if (!_options.IsSetConnectTimeout)
                    {
                        //client.Connect();
                        //while(client.Connected)
                        //channels.Add(client);
                    }
                    else
                    {
                        //client.Connect();
                        //if (task.Wait(_options.ConnectTimeout))
                        //{
                        //    channels.Add(client);
                        //}
                        //else
                        //{
                        //    client.Close();
                        //}
                    }
                }
                catch (SocketException) { }
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
            var config = new AsyncTcpSocketClientConfiguration()
            {
                ReceiveBufferSize = 32,
                SendBufferSize = 32,
                NoDelay = true,
            };

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
