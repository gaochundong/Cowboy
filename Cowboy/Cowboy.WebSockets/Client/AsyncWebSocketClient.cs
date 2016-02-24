using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.WebSockets.Buffer;
using Cowboy.WebSockets.Extensions;
using Cowboy.WebSockets.SubProtocols;

namespace Cowboy.WebSockets
{
    public sealed class AsyncWebSocketClient : IDisposable
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<AsyncWebSocketClient>();
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private readonly IAsyncWebSocketClientMessageDispatcher _dispatcher;
        private readonly AsyncWebSocketClientConfiguration _configuration;
        private readonly IFrameBuilder _frameBuilder = new WebSocketFrameBuilder();
        private IPEndPoint _remoteEndPoint;
        private Stream _stream;
        private byte[] _receiveBuffer;
        private int _receiveBufferOffset = 0;

        private readonly Uri _uri;
        private bool _sslEnabled = false;
        private string _secWebSocketKey;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _closing = 3;
        private const int _disposed = 5;

        private readonly SemaphoreSlim _keepAliveLocker = new SemaphoreSlim(1, 1);
        private KeepAliveTracker _keepAliveTracker;
        private Timer _keepAliveTimeoutTimer;
        private Timer _closingTimeoutTimer;

        #endregion

        #region Constructors

        public AsyncWebSocketClient(Uri uri, IAsyncWebSocketClientMessageDispatcher dispatcher, AsyncWebSocketClientConfiguration configuration = null)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            if (!Consts.WebSocketSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                throw new NotSupportedException(
                    string.Format("Not support the specified scheme [{0}].", uri.Scheme));

            _uri = uri;

            var host = _uri.Host;
            var port = _uri.Port > 0 ? _uri.Port : uri.Scheme.ToLowerInvariant() == "wss" ? 443 : 80;

            IPAddress ipAddress;
            if (IPAddress.TryParse(host, out ipAddress))
            {
                _remoteEndPoint = new IPEndPoint(ipAddress, port);
            }
            else
            {
                if (host.ToLowerInvariant() == "localhost")
                {
                    _remoteEndPoint = new IPEndPoint(IPAddress.Parse(@"127.0.0.1"), port);
                }
                else
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    if (addresses.Any())
                    {
                        _remoteEndPoint = new IPEndPoint(addresses.First(), port);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            string.Format("Cannot resolve host [{0}] by DNS.", host));
                    }
                }
            }

            _dispatcher = dispatcher;
            _configuration = configuration ?? new AsyncWebSocketClientConfiguration();
            _sslEnabled = uri.Scheme.ToLowerInvariant() == "wss";

            Initialize();
        }

        public AsyncWebSocketClient(Uri uri,
            Func<AsyncWebSocketClient, string, Task> onServerTextReceived = null,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerBinaryReceived = null,
            Func<AsyncWebSocketClient, Task> onServerConnected = null,
            Func<AsyncWebSocketClient, Task> onServerDisconnected = null,
            AsyncWebSocketClientConfiguration configuration = null)
            : this(uri,
                 new InternalAsyncWebSocketClientMessageDispatcherImplementation(
                     onServerTextReceived, onServerBinaryReceived, onServerConnected, onServerDisconnected),
                 configuration)
        {
        }

        public AsyncWebSocketClient(Uri uri,
            Func<AsyncWebSocketClient, string, Task> onServerTextReceived = null,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerBinaryReceived = null,
            Func<AsyncWebSocketClient, Task> onServerConnected = null,
            Func<AsyncWebSocketClient, Task> onServerDisconnected = null,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerFragmentationStreamOpened = null,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerFragmentationStreamContinued = null,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerFragmentationStreamClosed = null,
            AsyncWebSocketClientConfiguration configuration = null)
            : this(uri,
                 new InternalAsyncWebSocketClientMessageDispatcherImplementation(
                     onServerTextReceived, onServerBinaryReceived, onServerConnected, onServerDisconnected,
                     onServerFragmentationStreamOpened, onServerFragmentationStreamContinued, onServerFragmentationStreamClosed),
                 configuration)
        {
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialPooledBufferCount, _configuration.ReceiveBufferSize);
            _keepAliveTracker = KeepAliveTracker.Create(KeepAliveInterval, new TimerCallback((s) => OnKeepAlive()));
            _keepAliveTimeoutTimer = new Timer(new TimerCallback((s) => OnKeepAliveTimeout()), null, Timeout.Infinite, Timeout.Infinite);
            _closingTimeoutTimer = new Timer(new TimerCallback((s) => OnCloseTimeout()), null, Timeout.Infinite, Timeout.Infinite);
        }

        #endregion

        #region Properties

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
                    (IPEndPoint)_tcpClient.Client.LocalEndPoint : null;
            }
        }

        public Uri Uri { get { return _uri; } }

        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }
        public TimeSpan CloseTimeout { get { return _configuration.CloseTimeout; } }
        public TimeSpan KeepAliveInterval { get { return _configuration.KeepAliveInterval; } }
        public TimeSpan KeepAliveTimeout { get { return _configuration.KeepAliveTimeout; } }

        public IDictionary<string, IWebSocketExtensionNegotiator> EnabledExtensions { get { return _configuration.EnabledExtensions; } }
        public IDictionary<string, IWebSocketSubProtocolNegotiator> EnabledSubProtocols { get { return _configuration.EnabledSubProtocols; } }
        public IEnumerable<WebSocketExtensionOfferDescription> OfferedExtensions { get { return _configuration.OfferedExtensions; } }
        public IEnumerable<WebSocketSubProtocolRequestDescription> RequestedSubProtocols { get { return _configuration.RequestedSubProtocols; } }

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

        public override string ToString()
        {
            return string.Format("RemoteEndPoint[{0}], LocalEndPoint[{1}]",
                this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Connect

        public async Task Connect()
        {
            int origin = Interlocked.CompareExchange(ref _state, _connecting, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This websocket client has already connected to server.");
            }

            try
            {
                _tcpClient = new TcpClient();

                var awaiter = _tcpClient.ConnectAsync(_remoteEndPoint.Address, _remoteEndPoint.Port);
                if (!awaiter.Wait(ConnectTimeout))
                {
                    await Abort();
                    throw new TimeoutException(string.Format(
                        "Connect to [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
                }

                ConfigureClient();
                var negotiator = NegotiateStream(_tcpClient.GetStream());
                if (!negotiator.Wait(ConnectTimeout))
                {
                    await Close(WebSocketCloseCode.TlsHandshakeFailed, "SSL/TLS handshake timeout.");
                    throw new TimeoutException(string.Format(
                        "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", RemoteEndPoint, ConnectTimeout));
                }
                _stream = negotiator.Result;

                _receiveBuffer = _bufferManager.BorrowBuffer();
                _receiveBufferOffset = 0;

                var handshaker = OpenHandshake();
                if (!handshaker.Wait(ConnectTimeout))
                {
                    await Close(WebSocketCloseCode.ProtocolError, "Opening handshake timeout.");
                    throw new TimeoutException(string.Format(
                        "Handshake with remote [{0}] timeout [{1}].", RemoteEndPoint, ConnectTimeout));
                }
                if (!handshaker.Result)
                {
                    await Close(WebSocketCloseCode.ProtocolError, "Opening handshake failed.");
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed.", RemoteEndPoint));
                }

                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _log.DebugFormat("Connected to server [{0}] with dispatcher [{1}] on [{2}].",
                    this.RemoteEndPoint,
                    _dispatcher.GetType().Name,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));

                bool isErrorOccurredInUserSide = false;
                try
                {
                    await _dispatcher.OnServerConnected(this);
                }
                catch (Exception ex)
                {
                    isErrorOccurredInUserSide = true;
                    HandleUserSideError(ex);
                }

                if (!isErrorOccurredInUserSide)
                {
                    Task.Run(async () =>
                    {
                        _keepAliveTracker.StartTimer();
                        await Process();
                    })
                    .Forget();
                }
                else
                {
                    await Abort();
                }
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
                    BufferDeflector.ReplaceBuffer(_bufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);

                    while (true)
                    {
                        Header frameHeader = null;
                        if (_frameBuilder.TryDecodeFrameHeader(_receiveBuffer, _receiveBufferOffset, out frameHeader)
                            && frameHeader.Length + frameHeader.PayloadLength <= _receiveBufferOffset)
                        {
                            try
                            {
                                if (frameHeader.IsMasked)
                                {
                                    await Close(WebSocketCloseCode.ProtocolError, "A client MUST close a connection if it detects a masked frame.");
                                    throw new WebSocketException(string.Format(
                                        "Client received masked frame [{0}] from remote [{1}].", frameHeader.OpCode, RemoteEndPoint));
                                }

                                byte[] payload;
                                int payloadOffset;
                                int payloadCount;
                                _frameBuilder.DecodePayload(_receiveBuffer, frameHeader, out payload, out payloadOffset, out payloadCount);

                                switch (frameHeader.OpCode)
                                {
                                    case OpCode.Continuation:
                                        {
                                            if (!frameHeader.IsFIN)
                                            {
                                                try
                                                {
                                                    await _dispatcher.OnServerFragmentationStreamContinued(this, payload, payloadOffset, payloadCount);
                                                }
                                                catch (Exception ex)
                                                {
                                                    HandleUserSideError(ex);
                                                }
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    await _dispatcher.OnServerFragmentationStreamClosed(this, payload, payloadOffset, payloadCount);
                                                }
                                                catch (Exception ex)
                                                {
                                                    HandleUserSideError(ex);
                                                }
                                            }
                                        }
                                        break;
                                    case OpCode.Text:
                                        {
                                            if (frameHeader.IsFIN)
                                            {
                                                try
                                                {
                                                    var text = Encoding.UTF8.GetString(payload, payloadOffset, payloadCount);
                                                    await _dispatcher.OnServerTextReceived(this, text);
                                                }
                                                catch (Exception ex)
                                                {
                                                    HandleUserSideError(ex);
                                                }
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    await _dispatcher.OnServerFragmentationStreamOpened(this, payload, payloadOffset, payloadCount);
                                                }
                                                catch (Exception ex)
                                                {
                                                    HandleUserSideError(ex);
                                                }
                                            }
                                        }
                                        break;
                                    case OpCode.Binary:
                                        {
                                            if (frameHeader.IsFIN)
                                            {
                                                try
                                                {
                                                    await _dispatcher.OnServerBinaryReceived(this, payload, payloadOffset, payloadCount);
                                                }
                                                catch (Exception ex)
                                                {
                                                    HandleUserSideError(ex);
                                                }
                                            }
                                            else
                                            {
                                                try
                                                {
                                                    await _dispatcher.OnServerFragmentationStreamOpened(this, payload, payloadOffset, payloadCount);
                                                }
                                                catch (Exception ex)
                                                {
                                                    HandleUserSideError(ex);
                                                }
                                            }
                                        }
                                        break;
                                    case OpCode.Close:
                                        {
                                            if (!frameHeader.IsFIN)
                                            {
                                                throw new WebSocketException(string.Format(
                                                    "Client received unfinished frame [{0}] from remote [{1}].", frameHeader.OpCode, RemoteEndPoint));
                                            }

                                            if (payloadCount > 1)
                                            {
                                                var statusCode = payload[0] * 256 + payload[1];
                                                var closeCode = (WebSocketCloseCode)statusCode;
                                                var closeReason = string.Empty;

                                                if (payloadCount > 2)
                                                {
                                                    closeReason = Encoding.UTF8.GetString(payload, 2, payloadCount - 2);
                                                }
#if DEBUG
                                                _log.DebugFormat("Receive server side close frame [{0}] [{1}].", closeCode, closeReason);
#endif
                                                // If an endpoint receives a Close frame and did not previously send a
                                                // Close frame, the endpoint MUST send a Close frame in response.  (When
                                                // sending a Close frame in response, the endpoint typically echos the
                                                // status code it received.)  It SHOULD do so as soon as practical.
                                                await Close(closeCode, closeReason);
                                            }
                                            else
                                            {
#if DEBUG
                                                _log.DebugFormat("Receive server side close frame but no status code.");
#endif
                                                await Close(WebSocketCloseCode.InvalidPayloadData);
                                            }
                                        }
                                        break;
                                    case OpCode.Ping:
                                        {
                                            if (!frameHeader.IsFIN)
                                            {
                                                throw new WebSocketException(string.Format(
                                                    "Client received unfinished frame [{0}] from remote [{1}].", frameHeader.OpCode, RemoteEndPoint));
                                            }

                                            // Upon receipt of a Ping frame, an endpoint MUST send a Pong frame in
                                            // response, unless it already received a Close frame.  It SHOULD
                                            // respond with Pong frame as soon as is practical.  Pong frames are
                                            // discussed in Section 5.5.3.
                                            // 
                                            // An endpoint MAY send a Ping frame any time after the connection is
                                            // established and before the connection is closed.
                                            // 
                                            // A Ping frame may serve either as a keep-alive or as a means to
                                            // verify that the remote endpoint is still responsive.
                                            var ping = Encoding.UTF8.GetString(payload, payloadOffset, payloadCount);
#if DEBUG
                                            _log.DebugFormat("Receive server side ping frame [{0}].", ping);
#endif
                                            if (State == WebSocketState.Open)
                                            {
                                                // A Pong frame sent in response to a Ping frame must have identical
                                                // "Application data" as found in the message body of the Ping frame being replied to.
                                                var pong = new PongFrame(ping).ToArray(_frameBuilder);
                                                await SendFrame(pong);
#if DEBUG
                                                _log.DebugFormat("Send client side pong frame [{0}].", ping);
#endif
                                            }
                                        }
                                        break;
                                    case OpCode.Pong:
                                        {
                                            if (!frameHeader.IsFIN)
                                            {
                                                throw new WebSocketException(string.Format(
                                                    "Client received unfinished frame [{0}] from remote [{1}].", frameHeader.OpCode, RemoteEndPoint));
                                            }

                                            // If an endpoint receives a Ping frame and has not yet sent Pong
                                            // frame(s) in response to previous Ping frame(s), the endpoint MAY
                                            // elect to send a Pong frame for only the most recently processed Ping frame.
                                            // 
                                            // A Pong frame MAY be sent unsolicited.  This serves as a
                                            // unidirectional heartbeat.  A response to an unsolicited Pong frame is not expected.
                                            var pong = Encoding.UTF8.GetString(payload, payloadOffset, payloadCount);
                                            StopKeepAliveTimeoutTimer();
#if DEBUG
                                            _log.DebugFormat("Receive server side pong frame [{0}].", pong);
#endif
                                        }
                                        break;
                                    default:
                                        {
                                            // Incoming data MUST always be validated by both clients and servers.
                                            // If, at any time, an endpoint is faced with data that it does not
                                            // understand or that violates some criteria by which the endpoint
                                            // determines safety of input, or when the endpoint sees an opening
                                            // handshake that does not correspond to the values it is expecting
                                            // (e.g., incorrect path or origin in the client request), the endpoint
                                            // MAY drop the TCP connection.  If the invalid data was received after
                                            // a successful WebSocket handshake, the endpoint SHOULD send a Close
                                            // frame with an appropriate status code (Section 7.4) before proceeding
                                            // to _Close the WebSocket Connection_.  Use of a Close frame with an
                                            // appropriate status code can help in diagnosing the problem.  If the
                                            // invalid data is sent during the WebSocket handshake, the server
                                            // SHOULD return an appropriate HTTP [RFC2616] status code.
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
                            finally
                            {
                                try
                                {
                                    BufferDeflector.ShiftBuffer(_bufferManager, frameHeader.Length + frameHeader.PayloadLength, ref _receiveBuffer, ref _receiveBufferOffset);
                                }
                                catch (ArgumentOutOfRangeException) { }
                            }
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
            if (ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut)
            {
                _log.Error(ex.Message, ex);
                return false;
            }

            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException
                )
            {
                if (ex is SocketException)
                    _log.Error(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message), ex);

                return false;
            }

            _log.Error(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message), ex);
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
            if (!_sslEnabled)
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

            await sslStream.AuthenticateAsClientAsync(
                _configuration.SslTargetHost, // The name of the server that will share this SslStream.
                _configuration.SslClientCertificates, // The X509CertificateCollection that contains client certificates.
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
                var requestBuffer = WebSocketClientHandshaker.CreateOpenningHandshakeRequest(this, out _secWebSocketKey);
                await _stream.WriteAsync(requestBuffer, 0, requestBuffer.Length);

                int terminatorIndex = -1;
                while (!WebSocketHelpers.FindHeaderTerminator(_receiveBuffer, _receiveBufferOffset, out terminatorIndex))
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer, _receiveBufferOffset, _receiveBuffer.Length - _receiveBufferOffset);
                    if (receiveCount == 0)
                    {
                        throw new WebSocketHandshakeException(string.Format(
                            "Handshake with remote [{0}] failed due to receive zero bytes.", RemoteEndPoint));
                    }

                    BufferDeflector.ReplaceBuffer(_bufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);

                    if (_receiveBufferOffset > 2048)
                    {
                        throw new WebSocketHandshakeException(string.Format(
                            "Handshake with remote [{0}] failed due to receive weird stream.", RemoteEndPoint));
                    }
                }

                handshakeResult = WebSocketClientHandshaker.VerifyOpenningHandshakeResponse(this, _receiveBuffer, 0, terminatorIndex + Consts.HeaderTerminator.Length, _secWebSocketKey);

                BufferDeflector.ShiftBuffer(_bufferManager, terminatorIndex + Consts.HeaderTerminator.Length, ref _receiveBuffer, ref _receiveBufferOffset);
            }
            catch (ArgumentOutOfRangeException)
            {
                handshakeResult = false;
            }
            catch (WebSocketHandshakeException ex)
            {
                _log.Error(ex.Message, ex);
                handshakeResult = false;
            }

            return handshakeResult;
        }

        private void HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Session [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
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
                        var closingHandshake = new CloseFrame(closeCode, closeReason).ToArray(_frameBuilder);
                        try
                        {
                            if (_stream.CanWrite)
                            {
                                await _stream.WriteAsync(closingHandshake, 0, closingHandshake.Length);
                                StartClosingTimer();
#if DEBUG
                                _log.DebugFormat("Send client side close frame [{0}] [{1}].", closeCode, closeReason);
#endif
                            }
                        }
                        catch (Exception ex) when (!ShouldThrow(ex)) { }
                        return;
                    }
                case _connecting:
                case _closing:
                    {
                        await Close();
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
                if (_keepAliveTimeoutTimer != null)
                {
                    _keepAliveTimeoutTimer.Dispose();
                }
                if (_closingTimeoutTimer != null)
                {
                    _closingTimeoutTimer.Dispose();
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
            _receiveBufferOffset = 0;

            _log.DebugFormat("Disconnected from server [{0}] with dispatcher [{1}] on [{2}].",
                this.RemoteEndPoint,
                _dispatcher.GetType().Name,
                DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));
            try
            {
                await _dispatcher.OnServerDisconnected(this);
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        public async Task Abort()
        {
            await Close();
        }

        private void StartClosingTimer()
        {
            // In abnormal cases (such as not having received a TCP Close 
            // from the server after a reasonable amount of time) a client MAY initiate the TCP Close.
            _closingTimeoutTimer.Change((int)CloseTimeout.TotalMilliseconds, Timeout.Infinite);
        }

        private async void OnCloseTimeout()
        {
            // After both sending and receiving a Close message, an endpoint
            // considers the WebSocket connection closed and MUST close the
            // underlying TCP connection.  The server MUST close the underlying TCP
            // connection immediately; the client SHOULD wait for the server to
            // close the connection but MAY close the connection at any time after
            // sending and receiving a Close message, e.g., if it has not received a
            // TCP Close from the server in a reasonable time period.
            _log.WarnFormat("Closing timer timeout [{0}] then close automatically.", CloseTimeout);
            await Close();
        }

        #endregion

        #region Send

        public async Task SendTextAsync(string text)
        {
            await SendFrame(new TextFrame(text).ToArray(_frameBuilder));
        }

        public async Task SendBinaryAsync(byte[] data)
        {
            await SendBinaryAsync(data, 0, data.Length);
        }

        public async Task SendBinaryAsync(byte[] data, int offset, int count)
        {
            await SendFrame(new BinaryFrame(data, offset, count).ToArray(_frameBuilder));
        }

        public async Task SendBinaryAsync(ArraySegment<byte> segment)
        {
            await SendFrame(new BinaryFrame(segment).ToArray(_frameBuilder));
        }

        public async Task SendStreamAsync(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException("stream");

            int fragmentLength = _configuration.ReasonableFragmentSize;
            var buffer = new byte[fragmentLength];
            int readCount = 0;

            readCount = await stream.ReadAsync(buffer, 0, fragmentLength);
            if (readCount == 0)
                return;
            await SendFrame(new BinaryFragmentationFrame(OpCode.Binary, buffer, 0, readCount, isFin: false).ToArray(_frameBuilder));

            while (true)
            {
                readCount = await stream.ReadAsync(buffer, 0, fragmentLength);
                if (readCount != 0)
                {
                    await SendFrame(new BinaryFragmentationFrame(OpCode.Continuation, buffer, 0, readCount, isFin: false).ToArray(_frameBuilder));
                }
                else
                {
                    await SendFrame(new BinaryFragmentationFrame(OpCode.Continuation, buffer, 0, 0, isFin: true).ToArray(_frameBuilder));
                    break;
                }
            }
        }

        private async Task SendFrame(byte[] frame)
        {
            if (frame == null)
            {
                throw new ArgumentNullException("frame");
            }
            if (State != WebSocketState.Open)
            {
                throw new InvalidOperationException("This websocket client has not connected to server.");
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

        private void StartKeepAliveTimeoutTimer()
        {
            _keepAliveTimeoutTimer.Change((int)KeepAliveTimeout.TotalMilliseconds, Timeout.Infinite);
        }

        private void StopKeepAliveTimeoutTimer()
        {
            _keepAliveTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private async void OnKeepAliveTimeout()
        {
            _log.WarnFormat("Keep-alive timer timeout [{1}].", KeepAliveTimeout);
            await Close(WebSocketCloseCode.AbnormalClosure, "Keep-Alive Timeout");
        }

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
                        var keepAliveFrame = new PingFrame().ToArray(_frameBuilder);
                        await SendFrame(keepAliveFrame);
                        StartKeepAliveTimeoutTimer();
#if DEBUG
                        _log.DebugFormat("Send client side ping frame [{0}].", string.Empty);
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

        #region Extensions

        internal void AgreeExtensions(IEnumerable<string> extensions)
        {
            if (extensions == null)
                throw new ArgumentNullException("extensions");

            // If a server gives an invalid response, such as accepting a PMCE that
            // the client did not offer, the client MUST _Fail the WebSocket Connection_.
            if (this.OfferedExtensions == null
                || !this.OfferedExtensions.Any()
                || this.EnabledExtensions == null
                || !this.EnabledExtensions.Any())
                throw new WebSocketHandshakeException(string.Format(
                    "Negotiate extension with remote [{0}] failed due to no extension enabled.", this.RemoteEndPoint));

            // Note that the order of extensions is significant.  Any interactions
            // between multiple extensions MAY be defined in the documents defining
            // the extensions.  In the absence of such definitions, the
            // interpretation is that the header fields listed by the client in its
            // request represent a preference of the header fields it wishes to use,
            // with the first options listed being most preferable.  The extensions
            // listed by the server in response represent the extensions actually in
            // use for the connection.  Should the extensions modify the data and/or
            // framing, the order of operations on the data should be assumed to be
            // the same as the order in which the extensions are listed in the
            // server's response in the opening handshake.
            // For example, if there are two extensions "foo" and "bar" and if the
            // header field |Sec-WebSocket-Extensions| sent by the server has the
            // value "foo, bar", then operations on the data will be made as
            // bar(foo(data)), be those changes to the data itself (such as
            // compression) or changes to the framing that may "stack".
            var agreedExtensions = new SortedList<int, IWebSocketExtension>();
            var suggestedExtensions = string.Join(",", extensions).Split(',')
                .Select(p => p.TrimStart().TrimEnd()).Where(p => !string.IsNullOrWhiteSpace(p));

            int order = 0;
            foreach (var extension in suggestedExtensions)
            {
                order++;

                var offeredExtensionName = extension.Split(';').First();

                // Extensions not listed by the client MUST NOT be listed.
                if (!this.EnabledExtensions.ContainsKey(offeredExtensionName))
                    throw new WebSocketHandshakeException(string.Format(
                        "Negotiate extension with remote [{0}] failed due to un-enabled extensions [{1}].",
                        this.RemoteEndPoint, offeredExtensionName));

                var extensionNegotiator = this.EnabledExtensions[offeredExtensionName];

                string invalidParameter;
                IWebSocketExtension negotiatedExtension;
                if (!extensionNegotiator.NegotiateAsClient(extension, out invalidParameter, out negotiatedExtension)
                    || !string.IsNullOrEmpty(invalidParameter)
                    || negotiatedExtension == null)
                {
                    throw new WebSocketHandshakeException(string.Format(
                        "Negotiate extension with remote [{0}] failed due to extension [{1}] has invalid parameter [{2}].",
                        this.RemoteEndPoint, extension, invalidParameter));
                }

                agreedExtensions.Add(order, negotiatedExtension);
            }

            // If a server gives an invalid response, such as accepting a PMCE that
            // the client did not offer, the client MUST _Fail the WebSocket Connection_.
            foreach (var extension in agreedExtensions.Values)
            {
                if (!this.OfferedExtensions.Any(x => x.ExtensionNegotiationOffer.StartsWith(extension.Name)))
                    throw new WebSocketHandshakeException(string.Format(
                        "Negotiate extension with remote [{0}] failed due to extension [{1}] not be offered.",
                        this.RemoteEndPoint, extension.Name));
            }

            // A server MUST NOT accept a PMCE extension negotiation offer together
            // with another extension if the PMCE will conflict with the extension
            // on their use of the RSV1 bit.  A client that received a response
            // accepting a PMCE extension negotiation offer together with such an
            // extension MUST _Fail the WebSocket Connection_.
            bool isRsv1BitOccupied = false;
            bool isRsv2BitOccupied = false;
            bool isRsv3BitOccupied = false;
            foreach (var extension in agreedExtensions.Values)
            {
                if ((isRsv1BitOccupied && extension.Rsv1BitOccupied)
                    || (isRsv2BitOccupied && extension.Rsv2BitOccupied)
                    || (isRsv3BitOccupied && extension.Rsv3BitOccupied))
                    throw new WebSocketHandshakeException(string.Format(
                        "Negotiate extension with remote [{0}] failed due to conflict bit occupied.", this.RemoteEndPoint));

                isRsv1BitOccupied = isRsv1BitOccupied | extension.Rsv1BitOccupied;
                isRsv2BitOccupied = isRsv2BitOccupied | extension.Rsv2BitOccupied;
                isRsv3BitOccupied = isRsv3BitOccupied | extension.Rsv3BitOccupied;
            }

            _frameBuilder.NegotiatedExtensions = agreedExtensions;
        }

        #endregion

        #region Sub-Protocols

        internal void UseSubProtocol(string protocol)
        {
            if (string.IsNullOrWhiteSpace(protocol))
                throw new ArgumentNullException("protocol");

            if (this.RequestedSubProtocols == null
                || !this.RequestedSubProtocols.Any()
                || this.EnabledSubProtocols == null
                || !this.EnabledSubProtocols.Any())
                throw new WebSocketHandshakeException(string.Format(
                    "Negotiate sub-protocol with remote [{0}] failed due to sub-protocol [{1}] is not enabled.",
                    this.RemoteEndPoint, protocol));

            var requestedSubProtocols = string.Join(",", this.RequestedSubProtocols.Select(s => s.RequestedSubProtocol))
                .Split(',').Select(p => p.TrimStart().TrimEnd()).Where(p => !string.IsNullOrWhiteSpace(p));

            if (!requestedSubProtocols.Contains(protocol))
                throw new WebSocketHandshakeException(string.Format(
                    "Negotiate sub-protocol with remote [{0}] failed due to sub-protocol [{1}] has not been requested.",
                    this.RemoteEndPoint, protocol));

            // format : name.version.parameter
            var segements = protocol.Split('.')
                .Select(p => p.TrimStart().TrimEnd()).Where(p => !string.IsNullOrWhiteSpace(p))
                .ToArray();
            string protocolName = segements[0];
            string protocolVersion = segements.Length > 1 ? segements[1] : null;
            string protocolParameter = segements.Length > 2 ? segements[2] : null;

            if (!this.EnabledSubProtocols.ContainsKey(protocolName))
                throw new WebSocketHandshakeException(string.Format(
                    "Negotiate sub-protocol with remote [{0}] failed due to sub-protocol [{1}] is not enabled.",
                    this.RemoteEndPoint, protocolName));

            var subProtocolNegotiator = this.EnabledSubProtocols[protocolName];

            string invalidParameter;
            IWebSocketSubProtocol negotiatedSubProtocol;
            if (!subProtocolNegotiator.NegotiateAsClient(protocolName, protocolVersion, protocolParameter, out invalidParameter, out negotiatedSubProtocol)
                || !string.IsNullOrEmpty(invalidParameter)
                || negotiatedSubProtocol == null)
            {
                throw new WebSocketHandshakeException(string.Format(
                    "Negotiate sub-protocol with remote [{0}] failed due to sub-protocol [{1}] has invalid parameter [{2}].",
                    this.RemoteEndPoint, protocol, invalidParameter));
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_keepAliveTimeoutTimer")]
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_keepAliveLocker")]
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_closingTimeoutTimer")]
        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Close().Wait();
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message, ex);
                }
            }
        }

        #endregion
    }
}
