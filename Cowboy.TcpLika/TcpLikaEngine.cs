using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Cowboy.Buffer;

namespace Cowboy.TcpLika
{
    internal class TcpLikaEngine
    {
        private TcpLikaCommandLineOptions _options;
        private Action<string> _logger = (s) => { };
        private IBufferManager _bufferManager;

        public TcpLikaEngine(TcpLikaCommandLineOptions options, Action<string> logger = null)
        {
            if (options == null)
                throw new ArgumentNullException("options");
            _options = options;

            if (logger != null)
                _logger = logger;

            if (_options.IsSetConnections)
                _bufferManager = new GrowingByteBufferManager(_options.Connections, 256);
            else
                _bufferManager = new GrowingByteBufferManager(10, 256);
        }

        public void Start()
        {
            int threads = 1;
            int connections = 1;

            if (_options.IsSetThreads)
                threads = _options.Threads;
            if (threads > Environment.ProcessorCount)
                threads = Environment.ProcessorCount;

            if (_options.IsSetConnections)
                connections = _options.Connections;

            if (connections < threads)
                threads = connections;

            int connectionsPerThread = connections / threads;
            int connectionsRemainder = connections % threads;

            var tasks = new List<Task>();
            for (int p = 0; p < threads; p++)
            {
                var task = Task.Run(async () =>
                {
                    foreach (var remoteEP in _options.RemoteEndPoints)
                    {
                        await PerformLoad(connectionsPerThread, remoteEP);
                    }
                });
                tasks.Add(task);
            }
            if (connectionsRemainder > 0)
            {
                var task = Task.Run(async () =>
                {
                    foreach (var remoteEP in _options.RemoteEndPoints)
                    {
                        await PerformLoad(connectionsRemainder, remoteEP);
                    }
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
                var client = new TcpClient();
                Stream stream = null;
                try
                {
                    _logger(string.Format("Connecting to [{0}].", remoteEP));
                    await client.ConnectAsync(remoteEP.Address, remoteEP.Port);

                    stream = await NegotiateStream(client.GetStream(), remoteEP);

                    if (_options.IsSetWebSocket)
                    {
                        if (!await HandshakeWebSocket(
                            stream,
                            remoteEP.Address.ToString(),
                            _options.IsSetWebSocketPath ? _options.WebSocketPath : "/",
                            _options.IsSetWebSocketProtocol ? _options.WebSocketProtocol : null))
                        {
                            _logger(string.Format("Handshake failed with [{0}] from [{1}].", remoteEP, client.Client.LocalEndPoint));
                        }
                    }

                    channels.Add(client);
                    _logger(string.Format("Connected to [{0}] from [{1}].", remoteEP, client.Client.LocalEndPoint));
                }
                catch (Exception ex) when (!ShouldThrow(ex))
                {
                    _logger(string.Format("Connect to [{0}] error occurred [{1}].", remoteEP, ex.Message));

                    if (stream != null)
                    {
                        stream.Close();
                        stream = null;
                    }
                    if (client != null && client.Connected)
                    {
                        client.Dispose();
                    }
                }
            }

            if (_options.IsSetChannelLifetime && channels.Any())
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
                catch (Exception ex) when (!ShouldThrow(ex))
                {
                    _logger(string.Format("Closed to [{0}] error occurred [{1}].", remoteEP, ex.Message));
                }
            }
        }

        private async Task<bool> HandshakeWebSocket(Stream stream, string host, string path, string protocol = null)
        {
            var context = WebSocketHandshake.BuildHandeshakeContext(host, path, protocol: protocol);
            await stream.WriteAsync(context.RequestBuffer, context.RequestBufferOffset, context.RequestBufferCount);

            var receiveBuffer = _bufferManager.BorrowBuffer();
            var count = await stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);

            context.ResponseBuffer = receiveBuffer;
            context.ResponseBufferOffset = 0;
            context.ResponseBufferCount = count;
            var passedVerification = WebSocketHandshake.VerifyHandshake(context);

            _bufferManager.ReturnBuffer(receiveBuffer);

            return passedVerification;
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

        private async Task<Stream> NegotiateStream(Stream stream, IPEndPoint remoteEP)
        {
            if (!_options.IsSetSsl)
                return stream;

            var validateRemoteCertificate = new RemoteCertificateValidationCallback(
                (object sender,
                X509Certificate certificate,
                X509Chain chain,
                SslPolicyErrors sslPolicyErrors)
                =>
                {
                    if (sslPolicyErrors == SslPolicyErrors.None)
                        return true;

                    if (_options.IsSetSslPolicyErrorsBypassed)
                        return true;
                    else
                        _logger(string.Format("Error occurred when validating remote certificate: [{0}], [{1}].",
                            remoteEP, sslPolicyErrors));

                    return false;
                });
            var sslStream = new SslStream(
                stream,
                false,
                validateRemoteCertificate,
                null,
                EncryptionPolicy.RequireEncryption);

            await sslStream.AuthenticateAsClientAsync(
                _options.SslTargetHost,
                _options.SslClientCertificates,
                SslProtocols.Ssl3 | SslProtocols.Tls,
                false);

#if VERBOSE
            _logger(string.Format(
                "Ssl Stream: SslProtocol[{0}], IsServer[{1}], IsAuthenticated[{2}], IsEncrypted[{3}], IsSigned[{4}], IsMutuallyAuthenticated[{5}], "
                + "HashAlgorithm[{6}], HashStrength[{7}], KeyExchangeAlgorithm[{8}], KeyExchangeStrength[{9}], CipherAlgorithm[{10}], CipherStrength[{11}].",
                sslStream.SslProtocol,
                sslStream.IsServer,
                sslStream.IsAuthenticated,
                sslStream.IsEncrypted,
                sslStream.IsSigned,
                sslStream.IsMutuallyAuthenticated,
                sslStream.HashAlgorithm,
                sslStream.HashStrength,
                sslStream.KeyExchangeAlgorithm,
                sslStream.KeyExchangeStrength,
                sslStream.CipherAlgorithm,
                sslStream.CipherStrength));
#endif

            return sslStream;
        }

        private bool ShouldThrow(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException)
            {
                return false;
            }
            return false;
        }
    }
}
