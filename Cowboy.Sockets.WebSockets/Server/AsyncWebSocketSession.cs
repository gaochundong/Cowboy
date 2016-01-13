using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class AsyncWebSocketSession
    {
        private static readonly ILog _log = Logger.Get<AsyncWebSocketSession>();
        private TcpClient _tcpClient;
        private readonly AsyncWebSocketServerConfiguration _configuration;
        private readonly IBufferManager _bufferManager;
        private readonly AsyncWebSocketRouteResolver _routeResolver;
        private AsyncWebSocketServerModule _module;
        private readonly AsyncWebSocketServer _server;
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
        private const int _closing = 3;
        private const int _disposed = 5;

        private readonly SemaphoreSlim _keepAliveLocker = new SemaphoreSlim(1, 1);
        private KeepAliveTracker _keepAliveTracker;

        public AsyncWebSocketSession(
            TcpClient tcpClient,
            AsyncWebSocketServerConfiguration configuration,
            IBufferManager bufferManager,
            AsyncWebSocketRouteResolver routeResolver,
            AsyncWebSocketServer server)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");
            if (routeResolver == null)
                throw new ArgumentNullException("routeResolver");
            if (server == null)
                throw new ArgumentNullException("server");

            _tcpClient = tcpClient;
            _configuration = configuration;
            _bufferManager = bufferManager;
            _routeResolver = routeResolver;
            _server = server;

            _sessionKey = Guid.NewGuid().ToString();
            this.StartTime = DateTime.UtcNow;

            _remoteEndPoint = (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.RemoteEndPoint : null;
            _localEndPoint = (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.LocalEndPoint : null;

            _keepAliveTracker = KeepAliveTracker.Create(KeepAliveInterval, new TimerCallback((s) => OnKeepAlive()));
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
        public AsyncWebSocketServer Server { get { return _server; } }

        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }
        public TimeSpan KeepAliveInterval { get { return _configuration.KeepAliveInterval; } }

        public WebSocketState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return WebSocketState.None;
                    case _connecting:
                        return WebSocketState.Connecting;
                    case _connected:
                        return WebSocketState.Open;
                    case _closing:
                        return WebSocketState.Closing;
                    case _disposed:
                        return WebSocketState.Closed;
                    default:
                        return WebSocketState.Closed;
                }
            }
        }

        #region Start

        internal async Task Start()
        {
            int origin = Interlocked.CompareExchange(ref _state, _connecting, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This websocket socket session has already started.");
            }

            try
            {
                ConfigureClient();

                var negotiator = NegotiateStream(_tcpClient.GetStream());
                if (!negotiator.Wait(ConnectTimeout))
                {
                    await Close(WebSocketCloseCode.TlsHandshakeFailed, "SSL/TLS handshake timeout.");
                    throw new TimeoutException(string.Format(
                        "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", this.RemoteEndPoint, ConnectTimeout));
                }
                _stream = negotiator.Result;

                _receiveBuffer = _bufferManager.BorrowBuffer();
                _sessionBuffer = _bufferManager.BorrowBuffer();
                _sessionBufferCount = 0;

                var handshaker = OpenHandshake();
                if (!handshaker.Wait(ConnectTimeout))
                {
                    throw new TimeoutException(string.Format(
                        "Handshake with remote [{0}] timeout [{1}].", this.RemoteEndPoint, ConnectTimeout));
                }
                if (!handshaker.Result)
                {
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed.", this.RemoteEndPoint));
                }

                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _log.DebugFormat("Session started for [{0}] on [{1}] in module [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    this.StartTime.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    _module.GetType().Name,
                    this.Server.SessionCount);
                await _module.OnSessionStarted(this);

                _keepAliveTracker.StartTimer();

                await Process();
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            when (ex is TimeoutException || ex is WebSocketException)
            {
                _log.Error(ex.Message, ex);
                await Abort();
                throw;
            }
        }

        private async Task Process()
        {
            try
            {
                while (State == WebSocketState.Open || State == WebSocketState.Closing)
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    if (receiveCount == 0)
                        break;

                    _keepAliveTracker.OnDataReceived();
                    BufferDeflector.AppendBuffer(_bufferManager, ref _receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                    while (true)
                    {
                        var frameHeader = Frame.DecodeHeader(_sessionBuffer, _sessionBufferCount);
                        if (frameHeader != null && frameHeader.Length + frameHeader.PayloadLength <= _sessionBufferCount)
                        {
                            try
                            {
                                if (!frameHeader.IsMasked)
                                {
                                    await Close(WebSocketCloseCode.ProtocolError, "A server MUST close the connection upon receiving a frame that is not masked.");
                                    throw new WebSocketException(string.Format(
                                        "Server received unmasked frame [{0}] from remote [{1}].", frameHeader.OpCode, RemoteEndPoint));
                                }

                                var payload = Frame.DecodeMaskedPayload(_sessionBuffer, frameHeader.MaskingKeyOffset, frameHeader.Length, frameHeader.PayloadLength);

                                switch (frameHeader.OpCode)
                                {
                                    case OpCode.Continuation:
                                        break;
                                    case OpCode.Text:
                                        {
                                            var text = Encoding.UTF8.GetString(payload, 0, payload.Length);
                                            await _module.OnSessionTextReceived(this, text);
                                        }
                                        break;
                                    case OpCode.Binary:
                                        {
                                            await _module.OnSessionBinaryReceived(this, payload, 0, payload.Length);
                                        }
                                        break;
                                    case OpCode.Close:
                                        {
                                            var statusCode = payload[0] * 256 + payload[1];
                                            var closeCode = (WebSocketCloseCode)statusCode;
                                            var closeReason = string.Empty;

                                            if (payload.Length > 2)
                                            {
                                                closeReason = Encoding.UTF8.GetString(payload, 2, payload.Length - 2);
                                            }
#if DEBUG
                                            _log.DebugFormat("Receive client side close frame [{0}] [{1}].", closeCode, closeReason);
#endif
                                            await Close(closeCode, closeReason);
                                        }
                                        break;
                                    case OpCode.Ping:
                                        {
                                            var ping = Encoding.UTF8.GetString(payload, 0, payload.Length);
#if DEBUG
                                            _log.DebugFormat("Receive client side ping frame [{0}].", ping);
#endif
                                            var pong = new PongFrame(ping, false).ToArray();
                                            await SendFrame(pong);
#if DEBUG
                                            _log.DebugFormat("Send server side pong frame [{0}].", string.Empty);
#endif
                                        }
                                        break;
                                    case OpCode.Pong:
                                        {
                                            var pong = Encoding.UTF8.GetString(payload, 0, payload.Length);
#if DEBUG
                                            _log.DebugFormat("Receive client side pong frame [{0}].", pong);
#endif
                                        }
                                        break;
                                    default:
                                        {
                                            await Close(WebSocketCloseCode.InvalidMessageType);
                                            throw new NotSupportedException(
                                                string.Format("Not support received opcode [{0}].", (byte)frameHeader.OpCode));
                                        }
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex.Message, ex);
                                throw;
                            }

                            BufferDeflector.ShiftBuffer(_bufferManager, frameHeader.Length + frameHeader.PayloadLength, ref _sessionBuffer, ref _sessionBufferCount);
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            finally
            {
                await Abort();
            }
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

        private async Task<bool> OpenHandshake()
        {
            bool handshakeResult = false;

            try
            {
                int terminatorIndex = -1;
                while (!WebSocketHelpers.FindHeaderTerminator(_sessionBuffer, _sessionBufferCount, out terminatorIndex))
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    if (receiveCount == 0)
                    {
                        throw new WebSocketException(string.Format(
                            "Handshake with remote [{0}] failed due to receive zero bytes.", RemoteEndPoint));
                    }

                    BufferDeflector.AppendBuffer(_bufferManager, ref _receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                    if (_sessionBufferCount > 2048)
                    {
                        throw new WebSocketException(string.Format(
                            "Handshake with remote [{0}] failed due to receive weird stream.", RemoteEndPoint));
                    }
                }

                string secWebSocketKey = string.Empty;
                string path = string.Empty;
                string query = string.Empty;
                handshakeResult = WebSocketServerHandshaker.HandleOpenningHandshakeRequest(this,
                    _sessionBuffer, 0, terminatorIndex + Consts.HeaderTerminator.Length,
                    out secWebSocketKey, out path, out query);

                _module = _routeResolver.Resolve(path, query);
                if (_module == null)
                {
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed due to invalid url [{1}{2}].", RemoteEndPoint, path, query));
                }

                if (handshakeResult)
                {
                    var responseBuffer = WebSocketServerHandshaker.CreateOpenningHandshakeResponse(this, secWebSocketKey);
                    await _stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
                }

                BufferDeflector.ShiftBuffer(_bufferManager, terminatorIndex + Consts.HeaderTerminator.Length, ref _sessionBuffer, ref _sessionBufferCount);
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
                handshakeResult = false;
            }

            return handshakeResult;
        }

        public override string ToString()
        {
            return string.Format("SessionKey[{0}], RemoteEndPoint[{1}], LocalEndPoint[{2}]",
                this.SessionKey, this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Close

        public async Task Close(WebSocketCloseCode closeCode)
        {
            await Close(closeCode, null);
        }

        public async Task Close(WebSocketCloseCode closeCode, string closeReason)
        {
            if (State == WebSocketState.Closed || State == WebSocketState.None)
                return;

            var priorState = Interlocked.Exchange(ref _state, _closing);
            switch (priorState)
            {
                case _connected:
                    {
                        var closingHandshake = new CloseFrame(closeCode, closeReason, false).ToArray();
                        try
                        {
                            if (_stream.CanWrite)
                            {
                                await _stream.WriteAsync(closingHandshake, 0, closingHandshake.Length);
#if DEBUG
                                _log.DebugFormat("Send server side close frame [{0}] [{1}].", closeCode, closeReason);
#endif
                            }
                        }
                        catch (Exception ex) when (!ShouldThrow(ex)) { }
                        return;
                    }
                case _connecting:
                case _closing:
                    {
                        await Abort();
                        return;
                    }
                case _disposed:
                case _none:
                default:
                    return;
            }
        }

        private async Task Close()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
                if (_keepAliveTracker != null)
                {
                    _keepAliveTracker.Dispose();
                }
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
                _module.GetType().Name,
                this.Server.SessionCount - 1);
            await _module.OnSessionClosed(this);
        }

        public async Task Abort()
        {
            await Close();
        }

        #endregion

        #region Send

        public async Task SendText(string text)
        {
            await SendFrame(new TextFrame(text, false).ToArray());
        }

        public async Task SendBinary(byte[] data)
        {
            await SendBinary(data, 0, data.Length);
        }

        public async Task SendBinary(byte[] data, int offset, int count)
        {
            await SendFrame(new BinaryFrame(data, offset, count, false).ToArray());
        }

        public async Task SendBinary(ArraySegment<byte> segment)
        {
            await SendFrame(new BinaryFrame(segment, false).ToArray());
        }

        private async Task SendFrame(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }
            if (State != WebSocketState.Open)
            {
                throw new InvalidOperationException("This websocket session has not connected.");
            }

            try
            {
                if (_stream.CanWrite)
                {
                    await _stream.WriteAsync(frame, 0, frame.Length);
                    _keepAliveTracker.OnDataSent();
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        #endregion

        #region Keep Alive

        private async void OnKeepAlive()
        {
            if (await _keepAliveLocker.WaitAsync(0))
            {
                try
                {
                    if (State != WebSocketState.Open)
                        return;

                    if (_keepAliveTracker.ShouldSendKeepAlive())
                    {
                        var keepAliveFrame = new PingFrame(false).ToArray();
                        await SendFrame(keepAliveFrame);
#if DEBUG
                        _log.DebugFormat("Send server side ping frame [{0}].", string.Empty);
#endif
                        _keepAliveTracker.ResetTimer();
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message, ex);
                    await Close(WebSocketCloseCode.EndpointUnavailable);
                }
                finally
                {
                    _keepAliveLocker.Release();
                }
            }
        }

        #endregion
    }
}
