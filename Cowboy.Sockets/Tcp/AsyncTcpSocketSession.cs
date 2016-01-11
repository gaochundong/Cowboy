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
        private TcpClient _tcpClient;
        private readonly AsyncTcpSocketServerConfiguration _configuration;
        private readonly IBufferManager _bufferManager;
        private readonly IAsyncTcpSocketServerMessageDispatcher _dispatcher;
        private readonly AsyncTcpSocketServer _server;
        private readonly string _sessionKey;
        private Stream _stream;
        private byte[] _receiveBuffer;
        private byte[] _sessionBuffer;
        private int _sessionBufferCount = 0;
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _localEndPoint;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _disposed = 5;

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

            _remoteEndPoint = (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.RemoteEndPoint : null;
            _localEndPoint = (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.LocalEndPoint : null;
        }

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get; private set; }
        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.RemoteEndPoint : _remoteEndPoint;
            }
        }
        public IPEndPoint LocalEndPoint
        {
            get
            {
                return (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.LocalEndPoint : _localEndPoint;
            }
        }
        public AsyncTcpSocketServer Server { get { return _server; } }

        public TcpSocketState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return TcpSocketState.None;
                    case _connecting:
                        return TcpSocketState.Connecting;
                    case _connected:
                        return TcpSocketState.Connected;
                    case _disposed:
                        return TcpSocketState.Closed;
                    default:
                        return TcpSocketState.Closed;
                }
            }
        }

        internal async Task Start()
        {
            int origin = Interlocked.CompareExchange(ref _state, _connecting, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This tcp socket session has already started.");
            }

            try
            {
                ConfigureClient();

                var negotiatorTimeout = TimeSpan.FromSeconds(30);
                var negotiator = NegotiateStream(_tcpClient.GetStream());
                if (!negotiator.Wait(negotiatorTimeout))
                {
                    throw new TimeoutException(string.Format(
                        "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", this.RemoteEndPoint, negotiatorTimeout));
                }
                _stream = negotiator.Result;

                _receiveBuffer = _bufferManager.BorrowBuffer();
                _sessionBuffer = _bufferManager.BorrowBuffer();
                _sessionBufferCount = 0;

                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _log.DebugFormat("Session started for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    this.StartTime.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    _dispatcher.GetType().Name,
                    this.Server.SessionCount);
                await _dispatcher.OnSessionStarted(this);

                await Process();
            }
            catch (Exception ex)
            when (ex is TimeoutException)
            {
                _log.Error(ex.Message, ex);
                await Close();
            }
        }

        public async Task Close()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
                if (_tcpClient != null && _tcpClient.Connected)
                {
                    _tcpClient.Dispose();
                    _tcpClient = null;
                }
            }
            catch (Exception) { }

            if (_receiveBuffer != null)
                _bufferManager.ReturnBuffer(_receiveBuffer);
            if (_sessionBuffer != null)
                _bufferManager.ReturnBuffer(_sessionBuffer);

            _log.DebugFormat("Session closed for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                this.RemoteEndPoint,
                DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                _dispatcher.GetType().Name,
                this.Server.SessionCount - 1);
            await _dispatcher.OnSessionClosed(this);
        }

        private async Task Process()
        {
            try
            {
                while (State == TcpSocketState.Connected)
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    if (receiveCount == 0)
                        break;

                    if (!_configuration.Framing)
                    {
                        await _dispatcher.OnSessionDataReceived(this, _receiveBuffer, 0, receiveCount);
                    }
                    else
                    {
                        BufferDeflector.AppendBuffer(_bufferManager, ref _receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                        while (true)
                        {
                            var frameHeader = TcpFrameHeader.ReadHeader(_sessionBuffer);
                            if (TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize <= _sessionBufferCount)
                            {
                                await _dispatcher.OnSessionDataReceived(this, _sessionBuffer, TcpFrameHeader.HEADER_SIZE, frameHeader.PayloadSize);
                                BufferDeflector.ShiftBuffer(_bufferManager, TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize, ref _sessionBuffer, ref _sessionBufferCount);
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
                await Close();
            }
        }

        public async Task Send(byte[] data)
        {
            await Send(data, 0, data.Length);
        }

        public async Task Send(byte[] data, int offset, int count)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (State != TcpSocketState.Connected)
            {
                throw new InvalidOperationException("This session has not connected.");
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
            if (!_configuration.SslEnabled)
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
            return true;
        }

        public override string ToString()
        {
            return SessionKey;
        }
    }
}
