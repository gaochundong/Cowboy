using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public sealed class AsyncTcpSocketSession
    {
        private static readonly ILog _log = Logger.Get<AsyncTcpSocketSession>();
        private readonly TcpClient _tcpClient;
        private readonly SemaphoreSlim _opsLock = new SemaphoreSlim(1, 1);
        private bool _closed = false;
        private readonly AsyncTcpSocketServerConfiguration _configuration;
        private readonly IBufferManager _bufferManager;
        private readonly IAsyncTcpSocketServerMessageDispatcher _dispatcher;
        private readonly AsyncTcpSocketServer _server;
        private readonly string _sessionKey;
        private Stream _stream;

        public AsyncTcpSocketSession(
            TcpClient tcpClient,
            AsyncTcpSocketServerConfiguration configuration,
            IBufferManager bufferManager,
            IAsyncTcpSocketServerMessageDispatcher dispatcher,
            AsyncTcpSocketServer server)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");
            if (server == null)
                throw new ArgumentNullException("server");

            _tcpClient = tcpClient;
            _configuration = configuration;
            _bufferManager = bufferManager;
            _dispatcher = dispatcher;
            _server = server;

            _sessionKey = Guid.NewGuid().ToString();
            this.StartTime = DateTime.UtcNow;
        }

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get; private set; }
        public bool Connected { get { return _tcpClient != null && _tcpClient.Connected; } }
        public IPEndPoint RemoteEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.RemoteEndPoint : null; } }
        public IPEndPoint LocalEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.LocalEndPoint : null; } }
        public AsyncTcpSocketServer Server { get { return _server; } }

        internal async Task Start()
        {
            if (await _opsLock.WaitAsync(0))
            {
                try
                {
                    if (Connected)
                    {
                        _closed = false;

                        ConfigureClient();

                        var negotiatorTimeout = TimeSpan.FromSeconds(30);
                        var negotiator = NegotiateStream(_tcpClient.GetStream());
                        if (!negotiator.Wait(negotiatorTimeout))
                        {
                            var remote = this.RemoteEndPoint;
                            Close();

                            throw new TimeoutException(string.Format(
                                "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", remote, negotiatorTimeout));
                        }
                        _stream = negotiator.Result;

                        _log.DebugFormat("Session started for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                            this.RemoteEndPoint,
                            this.StartTime.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                            _dispatcher.GetType().Name,
                            this.Server.SessionCount);
                        await _dispatcher.OnSessionStarted(this);

                        await Process();
                    }
                }
                finally
                {
                    _opsLock.Release();
                }
            }
        }

        public void Close()
        {
            if (!_closed)
            {
                _closed = true;

                try
                {
                    if (_stream != null)
                    {
                        _stream.Close();
                        _stream = null;
                    }
                    if (_tcpClient != null && _tcpClient.Connected)
                    {
                        _tcpClient.Close();
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message, ex);
                }
            }
        }

        private async Task Process()
        {
            if (!Connected)
                return;

            byte[] receiveBuffer = _bufferManager.BorrowBuffer();
            byte[] sessionBuffer = _bufferManager.BorrowBuffer();
            int sessionBufferCount = 0;

            try
            {
                while (Connected)
                {
                    int receiveCount = await _stream.ReadAsync(receiveBuffer, 0, receiveBuffer.Length);
                    if (receiveCount == 0)
                        break;

                    if (!_configuration.Framing)
                    {
                        await _dispatcher.OnSessionDataReceived(this, receiveBuffer, 0, receiveCount);
                    }
                    else
                    {
                        BufferDeflector.AppendBuffer(_bufferManager, ref receiveBuffer, receiveCount, ref sessionBuffer, ref sessionBufferCount);

                        while (true)
                        {
                            var frameHeader = TcpFrameHeader.ReadHeader(sessionBuffer);
                            if (TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize <= sessionBufferCount)
                            {
                                await _dispatcher.OnSessionDataReceived(this, sessionBuffer, TcpFrameHeader.HEADER_SIZE, frameHeader.PayloadSize);
                                BufferDeflector.ShiftBuffer(_bufferManager, TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize, ref sessionBuffer, ref sessionBufferCount);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            finally
            {
                _bufferManager.ReturnBuffer(receiveBuffer);
                _bufferManager.ReturnBuffer(sessionBuffer);

                _log.DebugFormat("Session closed for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    _dispatcher.GetType().Name,
                    this.Server.SessionCount - 1);

                Close();

                await _dispatcher.OnSessionClosed(this);
            }
        }

        public async Task Send(byte[] data)
        {
            await Send(data, 0, data.Length);
        }

        public async Task Send(byte[] data, int offset, int count)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (!Connected)
            {
                throw new InvalidProgramException("This session has been closed.");
            }

            try
            {
                if (_stream.CanWrite)
                {
                    if (!_configuration.Framing)
                    {
                        await _stream.WriteAsync(data, offset, count);
                    }
                    else
                    {
                        var frame = TcpFrame.FromPayload(data, offset, count);
                        var frameBuffer = frame.ToArray();
                        await _stream.WriteAsync(frameBuffer, 0, frameBuffer.Length);
                    }
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        private void ConfigureClient()
        {
            _tcpClient.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            _tcpClient.SendBufferSize = _configuration.SendBufferSize;
            _tcpClient.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
            _tcpClient.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
            _tcpClient.NoDelay = _configuration.NoDelay;
            _tcpClient.LingerState = _configuration.LingerState;
        }

        private async Task<Stream> NegotiateStream(Stream stream)
        {
            if (!_configuration.UseSsl)
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

                    if (_configuration.SslPolicyErrorsBypassed)
                        return true;
                    else
                        _log.ErrorFormat("Error occurred when validating remote certificate: [{0}], [{1}].",
                            this.RemoteEndPoint, sslPolicyErrors);

                    return false;
                });

            var sslStream = new SslStream(
                stream,
                false,
                validateRemoteCertificate,
                null,
                _configuration.SslEncryptionPolicy);

            await sslStream.AuthenticateAsServerAsync(
                _configuration.SslServerCertificate, // The X509Certificate used to authenticate the server.
                _configuration.SslClientCertificateRequired, // A Boolean value that specifies whether the client must supply a certificate for authentication.
                _configuration.SslEnabledProtocols, // The SslProtocols value that represents the protocol used for authentication.
                _configuration.SslCheckCertificateRevocation); // A Boolean value that specifies whether the certificate revocation list is checked during authentication.

            // When authentication succeeds, you must check the IsEncrypted and IsSigned properties 
            // to determine what security services are used by the SslStream. 
            // Check the IsMutuallyAuthenticated property to determine whether mutual authentication occurred.
            _log.DebugFormat(
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
                sslStream.CipherStrength);

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

        public override string ToString()
        {
            return SessionKey;
        }
    }
}
