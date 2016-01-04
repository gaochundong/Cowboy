using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.TcpLika
{
    internal class TcpLikaEngine
    {
        private TcpLikaCommandLineOptions _options;
        private Action<string> _logger = (s) => { };

        public TcpLikaEngine(TcpLikaCommandLineOptions options, Action<string> logger = null)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            _options = options;

            if (logger != null)
                _logger = logger;
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
                    await PerformLoad(connectionsPerThread, _options.RemoteEndPoints.First());
                });
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());
        }

        private async Task PerformLoad(int connections, IPEndPoint remoteEP)
        {
            var channels = new List<TcpClient>();

            for (int c = 0; c < connections; c++)
            {
                try
                {
                    var client = new TcpClient();
                    if (!_options.IsSetConnectTimeout)
                    {
                        _logger(string.Format("Connecting to [{0}].", remoteEP));
                        client.Connect(remoteEP);

                        if (_options.IsSetWebSocket)
                        {
                            HandshakeWebSocket(client, remoteEP);
                        }

                        channels.Add(client);
                        _logger(string.Format("Connected to [{0}] from [{1}].", remoteEP, client.Client.LocalEndPoint));
                    }
                    else
                    {
                        _logger(string.Format("Connecting to [{0}].", remoteEP));
                        var task = client.ConnectAsync(remoteEP.Address, remoteEP.Port);
                        if (task.Wait(_options.ConnectTimeout))
                        {
                            if (_options.IsSetWebSocket)
                            {
                                HandshakeWebSocket(client, remoteEP);
                            }

                            channels.Add(client);
                            _logger(string.Format("Connected to [{0}] from [{1}].", remoteEP, client.Client.LocalEndPoint));
                        }
                        else
                        {
                            _logger(string.Format("Connect to [{0}] timeout [{1}].", remoteEP, _options.ConnectTimeout));
                            client.Close();
                        }
                    }
                }
                catch (SocketException ex)
                {
                    _logger(string.Format("Connect to [{0}] error occurred [{1}].", remoteEP, ex.Message));
                }
            }

            if (_options.IsSetChannelLifetime)
            {
                await Task.Delay(_options.ChannelLifetime);
            }

            foreach (var client in channels)
            {
                try
                {
                    _logger(string.Format("Closed to [{0}] from [{1}].", remoteEP, client.Client.LocalEndPoint));
                    client.Close();
                }
                catch (SocketException ex)
                {
                    _logger(string.Format("Closed to [{0}] error occurred [{1}].", remoteEP, ex.Message));
                }
            }
        }

        private void HandshakeWebSocket(TcpClient client, IPEndPoint remoteEP)
        {
            var context = WebSocketHandshake.BuildHandeshakeContext(remoteEP.Address.ToString(), "/");
            client.GetStream().Write(context.RequestBuffer, context.RequestBufferOffset, context.RequestBufferCount);

            var receiveBuffer = new byte[8192];
            var count = client.GetStream().Read(receiveBuffer, 0, receiveBuffer.Length);

            context.ResponseBuffer = receiveBuffer;
            context.ResponseBufferOffset = 0;
            context.ResponseBufferCount = count;
            var passedVerification = WebSocketHandshake.VerifyHandshake(context);
        }

        private void ConfigureClient(TcpClient client)
        {
            client.ReceiveBufferSize = 32;
            client.SendBufferSize = 32;
            client.ExclusiveAddressUse = true;
            client.NoDelay = true;

            if (_options.IsSetNagle)
                client.NoDelay = _options.Nagle;

            if (_options.IsSetReceiveBufferSize)
                client.ReceiveBufferSize = _options.ReceiveBufferSize;

            if (_options.IsSetSendBufferSize)
                client.SendBufferSize = _options.SendBufferSize;
        }
    }
}
