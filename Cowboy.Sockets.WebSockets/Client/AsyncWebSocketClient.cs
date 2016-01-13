using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    public class AsyncWebSocketClient
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<AsyncWebSocketClient>();
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private readonly IAsyncWebSocketClientMessageDispatcher _dispatcher;
        private readonly AsyncWebSocketClientConfiguration _configuration;
        private IPEndPoint _remoteEndPoint;
        private Stream _stream;
        private byte[] _receiveBuffer;
        private byte[] _sessionBuffer;
        private int _sessionBufferCount = 0;

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
        private Timer _closingTimer;

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

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);
            _keepAliveTracker = KeepAliveTracker.Create(KeepAliveInterval, new TimerCallback((s) => OnKeepAlive()));
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
                _sessionBuffer = _bufferManager.BorrowBuffer();
                _sessionBufferCount = 0;

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
                await _dispatcher.OnServerConnected(this);

                _keepAliveTracker.StartTimer();

                Task.Run(async () =>
                {
                    await Process();
                })
                .Forget();
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
                                if (frameHeader.IsMasked)
                                {
                                    await Close(WebSocketCloseCode.ProtocolError, "A client MUST close a connection if it detects a masked frame.");
                                    throw new WebSocketException(string.Format(
                                        "Client received masked frame [{0}] from remote [{1}].", frameHeader.OpCode, RemoteEndPoint));
                                }

                                switch (frameHeader.OpCode)
                                {
                                    case OpCode.Continuation:
                                        break;
                                    case OpCode.Text:
                                        {
                                            var text = Encoding.UTF8.GetString(_sessionBuffer, frameHeader.Length, frameHeader.PayloadLength);
                                            await _dispatcher.OnServerTextReceived(this, text);
                                        }
                                        break;
                                    case OpCode.Binary:
                                        {
                                            await _dispatcher.OnServerBinaryReceived(this, _sessionBuffer, frameHeader.Length, frameHeader.PayloadLength);
                                        }
                                        break;
                                    case OpCode.Close:
                                        {
                                            var statusCode = _sessionBuffer[frameHeader.Length] * 256 + _sessionBuffer[frameHeader.Length + 1];
                                            var closeCode = (WebSocketCloseCode)statusCode;
                                            var closeReason = string.Empty;

                                            if (frameHeader.PayloadLength > 2)
                                            {
                                                closeReason = Encoding.UTF8.GetString(_sessionBuffer, frameHeader.Length + 2, frameHeader.PayloadLength - 2);
                                            }
#if DEBUG
                                            _log.DebugFormat("Receive server side close frame [{0}] [{1}].", closeCode, closeReason);
#endif
                                            await Close(closeCode, closeReason);
                                        }
                                        break;
                                    case OpCode.Ping:
                                        {
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
                                            var ping = Encoding.UTF8.GetString(_sessionBuffer, frameHeader.Length, frameHeader.PayloadLength);
#if DEBUG
                                            _log.DebugFormat("Receive server side ping frame [{0}].", ping);
#endif
                                            if (State == WebSocketState.Open)
                                            {
                                                // A Pong frame sent in response to a Ping frame must have identical
                                                // "Application data" as found in the message body of the Ping frame being replied to.
                                                var pong = new PongFrame(ping).ToArray();
                                                await SendFrame(pong);
#if DEBUG
                                                _log.DebugFormat("Send client side pong frame [{0}].", string.Empty);
#endif
                                            }
                                        }
                                        break;
                                    case OpCode.Pong:
                                        {
                                            // If an endpoint receives a Ping frame and has not yet sent Pong
                                            // frame(s) in response to previous Ping frame(s), the endpoint MAY
                                            // elect to send a Pong frame for only the most recently processed Ping frame.
                                            // 
                                            // A Pong frame MAY be sent unsolicited.  This serves as a
                                            // unidirectional heartbeat.  A response to an unsolicited Pong frame is not expected.
                                            var pong = Encoding.UTF8.GetString(_sessionBuffer, frameHeader.Length, frameHeader.PayloadLength);
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
                while (!WebSocketHelpers.FindHeaderTerminator(_sessionBuffer, _sessionBufferCount, out terminatorIndex))
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    if (receiveCount == 0)
                    {
                        throw new WebSocketHandshakeException(string.Format(
                            "Handshake with remote [{0}] failed due to receive zero bytes.", RemoteEndPoint));
                    }

                    BufferDeflector.AppendBuffer(_bufferManager, ref _receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                    if (_sessionBufferCount > 2048)
                    {
                        throw new WebSocketHandshakeException(string.Format(
                            "Handshake with remote [{0}] failed due to receive weird stream.", RemoteEndPoint));
                    }
                }

                handshakeResult = WebSocketClientHandshaker.VerifyOpenningHandshakeResponse(this, _sessionBuffer, 0, terminatorIndex + Consts.HeaderTerminator.Length, _secWebSocketKey);

                BufferDeflector.ShiftBuffer(_bufferManager, terminatorIndex + Consts.HeaderTerminator.Length, ref _sessionBuffer, ref _sessionBufferCount);
            }
            catch (WebSocketHandshakeException ex)
            {
                _log.Error(ex.Message, ex);
                handshakeResult = false;
            }

            return handshakeResult;
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
                        var closingHandshake = new CloseFrame(closeCode, closeReason).ToArray();
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
                if (_closingTimer != null)
                {
                    _closingTimer.Dispose();
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

            _log.DebugFormat("Disconnected from server [{0}] with dispatcher [{1}] on [{2}].",
                this.RemoteEndPoint,
                _dispatcher.GetType().Name,
                DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));
            await _dispatcher.OnServerDisconnected(this);
        }

        public async Task Abort()
        {
            await Close();
        }

        private void StartClosingTimer()
        {
            // In abnormal cases (such as not having received a TCP Close 
            // from the server after a reasonable amount of time) a client MAY initiate the TCP Close.
            _closingTimer = new Timer(new TimerCallback((s) => OnClose()), null, Timeout.Infinite, Timeout.Infinite);
            _closingTimer.Change((int)CloseTimeout.TotalMilliseconds, Timeout.Infinite);
        }

        private async void OnClose()
        {
            _log.WarnFormat("Closing timer timeout [{0}] then close automatically.", CloseTimeout);
            await Close();
        }

        #endregion

        #region Send

        public async Task SendText(string text)
        {
            await SendFrame(new TextFrame(text).ToArray());
        }

        public async Task SendBinary(byte[] data)
        {
            await SendBinary(data, 0, data.Length);
        }

        public async Task SendBinary(byte[] data, int offset, int count)
        {
            await SendFrame(new BinaryFrame(data, offset, count).ToArray());
        }

        public async Task SendBinary(ArraySegment<byte> segment)
        {
            await SendFrame(new BinaryFrame(segment).ToArray());
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
                        var keepAliveFrame = new PingFrame().ToArray();
                        await SendFrame(keepAliveFrame);
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
    }
}
