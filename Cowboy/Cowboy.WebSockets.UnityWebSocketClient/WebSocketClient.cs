using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Cowboy.WebSockets
{
    public sealed class WebSocketClient : IDisposable
    {
        #region Fields

        private Action<string> _log;
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private readonly WebSocketClientConfiguration _configuration;
        private readonly IFrameBuilder _frameBuilder = new WebSocketFrameBuilder();
        private IPEndPoint _remoteEndPoint = null;
        private IPEndPoint _localEndPoint = null;
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

        private readonly Semaphore _keepAliveLocker = new Semaphore(1, 1);
        private KeepAliveTracker _keepAliveTracker;
        private Timer _keepAliveTimeoutTimer;
        private Timer _closingTimeoutTimer;

        #endregion

        #region Constructors

        public WebSocketClient(Uri uri, WebSocketClientConfiguration configuration = null, Action<string> log = null)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (!Consts.WebSocketSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                throw new NotSupportedException(
                    string.Format("Not support the specified scheme [{0}].", uri.Scheme));

            _uri = uri;
            _remoteEndPoint = ResolveRemoteEndPoint(_uri);
            _configuration = configuration != null ? configuration : new WebSocketClientConfiguration();
            _log = log != null ? log : (s) => { };
            _sslEnabled = uri.Scheme.ToLowerInvariant() == "wss";

            Initialize();
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialPooledBufferCount, _configuration.ReceiveBufferSize);
            _keepAliveTracker = KeepAliveTracker.Create(KeepAliveInterval, new TimerCallback((s) => OnKeepAlive()));
            _keepAliveTimeoutTimer = new Timer(new TimerCallback((s) => OnKeepAliveTimeout()), null, Timeout.Infinite, Timeout.Infinite);
            _closingTimeoutTimer = new Timer(new TimerCallback((s) => OnCloseTimeout()), null, Timeout.Infinite, Timeout.Infinite);
        }

        private IPEndPoint ResolveRemoteEndPoint(Uri uri)
        {
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : uri.Scheme.ToLowerInvariant() == "wss" ? 443 : 80;

            IPAddress ipAddress;
            if (IPAddress.TryParse(host, out ipAddress))
            {
                return new IPEndPoint(ipAddress, port);
            }
            else
            {
                if (host.ToLowerInvariant() == "localhost")
                {
                    return new IPEndPoint(IPAddress.Parse(@"127.0.0.1"), port);
                }
                else
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    if (addresses.Length > 0)
                    {
                        return new IPEndPoint(addresses[0], port);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            string.Format("Cannot resolve host [{0}] by DNS.", host));
                    }
                }
            }
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
                    (IPEndPoint)_tcpClient.Client.LocalEndPoint : _localEndPoint;
            }
        }

        public Uri Uri { get { return _uri; } }

        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }
        public TimeSpan CloseTimeout { get { return _configuration.CloseTimeout; } }
        public TimeSpan KeepAliveInterval { get { return _configuration.KeepAliveInterval; } }
        public TimeSpan KeepAliveTimeout { get { return _configuration.KeepAliveTimeout; } }

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

        public void Connect()
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

            _tcpClient = _localEndPoint != null ? new TcpClient(_localEndPoint) : new TcpClient();

            _receiveBuffer = _bufferManager.BorrowBuffer();
            _receiveBufferOffset = 0;

            var ar = _tcpClient.BeginConnect(_remoteEndPoint.Address, _remoteEndPoint.Port, null, _tcpClient);
            if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
            {
                Abort();
                throw new TimeoutException(string.Format(
                    "Connect to [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
            }
            _tcpClient.EndConnect(ar);

            HandleTcpServerConnected();
        }

        private void HandleTcpServerConnected()
        {
            try
            {
                ConfigureClient();

                _stream = NegotiateStream(_tcpClient.GetStream());

                var handshaked = OpenHandshake();
                if (!handshaked)
                {
                    Close(WebSocketCloseCode.ProtocolError, "Opening handshake failed.");
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed.", RemoteEndPoint));
                }

                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _log(string.Format("Connected to server [{0}] on [{1}].",
                    this.RemoteEndPoint, DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff")));

                bool isErrorOccurredInUserSide = false;
                try
                {
                    RaiseServerConnected();
                }
                catch (Exception ex)
                {
                    isErrorOccurredInUserSide = true;
                    HandleUserSideError(ex);
                }

                if (!isErrorOccurredInUserSide)
                {
                    _keepAliveTracker.StartTimer();
                    ContinueReadBuffer();
                }
                else
                {
                    Abort();
                }
            }
            catch (ObjectDisposedException) { }
            catch (TimeoutException ex)
            {
                _log(ex.Message);
                Abort();
                throw;
            }
            catch (WebSocketException ex)
            {
                _log(ex.Message);
                Abort();
                throw;
            }
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

        private Stream NegotiateStream(Stream stream)
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
                        _log(string.Format("Error occurred when validating remote certificate: [{0}], [{1}].",
                            this.RemoteEndPoint, sslPolicyErrors));

                    return false;
                });

            var sslStream = new SslStream(
                stream,
                false,
                validateRemoteCertificate,
                null);

            var ar = sslStream.BeginAuthenticateAsClient(
                _configuration.SslTargetHost, // The name of the server that will share this SslStream.
                _configuration.SslClientCertificates, // The X509CertificateCollection that contains client certificates.
                _configuration.SslEnabledProtocols, // The SslProtocols value that represents the protocol used for authentication.
                _configuration.SslCheckCertificateRevocation, // A Boolean value that specifies whether the certificate revocation list is checked during authentication.
                null, _tcpClient);
            if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
            {
                Close(WebSocketCloseCode.TlsHandshakeFailed, "SSL/TLS handshake timeout.");
                throw new TimeoutException(string.Format(
                    "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", this.RemoteEndPoint, ConnectTimeout));
            }
            sslStream.EndAuthenticateAsClient(ar);

            // When authentication succeeds, you must check the IsEncrypted and IsSigned properties 
            // to determine what security services are used by the SslStream. 
            // Check the IsMutuallyAuthenticated property to determine whether mutual authentication occurred.
            _log(string.Format(
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

            return sslStream;
        }

        private bool OpenHandshake()
        {
            bool handshakeResult = false;

            try
            {
                var requestBuffer = WebSocketClientHandshaker.CreateOpenningHandshakeRequest(this, out _secWebSocketKey);
                var ar = _stream.BeginWrite(requestBuffer, 0, requestBuffer.Length, null, _stream);
                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
                {
                    Close(WebSocketCloseCode.ProtocolError, "Opening handshake timeout.");
                    throw new TimeoutException(string.Format(
                        "Handshake with remote [{0}] timeout [{1}].", RemoteEndPoint, ConnectTimeout));
                }
                _stream.EndWrite(ar);

                int terminatorIndex = -1;
                while (!WebSocketHelpers.FindHeaderTerminator(_receiveBuffer, _receiveBufferOffset, out terminatorIndex))
                {
                    ar = _stream.BeginRead(_receiveBuffer, _receiveBufferOffset, _receiveBuffer.Length - _receiveBufferOffset, null, _stream);
                    if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
                    {
                        Close(WebSocketCloseCode.ProtocolError, "Opening handshake timeout.");
                        throw new TimeoutException(string.Format(
                            "Handshake with remote [{0}] timeout [{1}].", RemoteEndPoint, ConnectTimeout));
                    }

                    int receiveCount = _stream.EndRead(ar);
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
                _log(ex.Message);
                handshakeResult = false;
            }

            return handshakeResult;
        }

        #endregion

        #region Receive

        private void ContinueReadBuffer()
        {
            if (State == WebSocketState.Open || State == WebSocketState.Closing)
            {
                try
                {
                    _stream.BeginRead(_receiveBuffer, _receiveBufferOffset, _receiveBuffer.Length - _receiveBufferOffset, HandleDataReceived, _tcpClient);
                }
                catch (Exception ex)
                {
                    if (!CloseIfShould(ex))
                        throw;
                }
            }
        }

        private void HandleDataReceived(IAsyncResult ar)
        {
            try
            {
                int numberOfReadBytes = 0;
                try
                {
                    // The EndRead method blocks until data is available. The EndRead method reads 
                    // as much data as is available up to the number of bytes specified in the size 
                    // parameter of the BeginRead method. If the remote host shuts down the Socket 
                    // connection and all available data has been received, the EndRead method 
                    // completes immediately and returns zero bytes.
                    numberOfReadBytes = _stream.EndRead(ar);
                }
                catch (Exception)
                {
                    // unable to read data from transport connection, 
                    // the existing connection was forcibly closes by remote host
                    numberOfReadBytes = 0;
                }

                if (numberOfReadBytes == 0)
                {
                    // connection has been closed
                    Abort();
                    return;
                }

                ReceiveBuffer(numberOfReadBytes);

                ContinueReadBuffer();
            }
            catch (Exception ex)
            {
                if (!CloseIfShould(ex))
                    throw;
            }
        }

        private void ReceiveBuffer(int receiveCount)
        {
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
                            Close(WebSocketCloseCode.ProtocolError, "A client MUST close a connection if it detects a masked frame.");
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
                                    throw new WebSocketException(string.Format(
                                        "Client received continuation opcode [{0}] from remote [{1}] but not supported.", frameHeader.OpCode, RemoteEndPoint));
                                }
                            case OpCode.Text:
                                {
                                    if (frameHeader.IsFIN)
                                    {
                                        try
                                        {
                                            var text = Encoding.UTF8.GetString(payload, payloadOffset, payloadCount);
                                            RaiseServerTextReceived(text);
                                        }
                                        catch (Exception ex)
                                        {
                                            HandleUserSideError(ex);
                                        }
                                    }
                                    else
                                    {
                                        throw new WebSocketException(string.Format(
                                            "Client received continuation opcode [{0}] from remote [{1}] but not supported.", frameHeader.OpCode, RemoteEndPoint));
                                    }
                                }
                                break;
                            case OpCode.Binary:
                                {
                                    if (frameHeader.IsFIN)
                                    {
                                        try
                                        {
                                            RaiseServerBinaryReceived(payload, payloadOffset, payloadCount);
                                        }
                                        catch (Exception ex)
                                        {
                                            HandleUserSideError(ex);
                                        }
                                    }
                                    else
                                    {
                                        throw new WebSocketException(string.Format(
                                            "Client received continuation opcode [{0}] from remote [{1}] but not supported.", frameHeader.OpCode, RemoteEndPoint));
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

                                        // If an endpoint receives a Close frame and did not previously send a
                                        // Close frame, the endpoint MUST send a Close frame in response.  (When
                                        // sending a Close frame in response, the endpoint typically echos the
                                        // status code it received.)  It SHOULD do so as soon as practical.
                                        Close(closeCode, closeReason);
                                    }
                                    else
                                    {
                                        Close(WebSocketCloseCode.InvalidPayloadData);
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

                                    if (State == WebSocketState.Open)
                                    {
                                        // A Pong frame sent in response to a Ping frame must have identical
                                        // "Application data" as found in the message body of the Ping frame being replied to.
                                        var pong = new PongFrame(ping).ToArray(_frameBuilder);
                                        SendFrame(pong);
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
                                    Close(WebSocketCloseCode.InvalidMessageType);
                                    throw new NotSupportedException(
                                        string.Format("Not support received opcode [{0}].", (byte)frameHeader.OpCode));
                                }
                        }
                    }
                    catch (Exception ex)
                    {
                        _log(ex.Message);
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

        #endregion

        #region Close

        public void Close(WebSocketCloseCode closeCode)
        {
            Close(closeCode, null);
        }

        public void Close(WebSocketCloseCode closeCode, string closeReason)
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
                                StartClosingTimer();
                                var ar = _stream.BeginWrite(closingHandshake, 0, closingHandshake.Length, null, _stream);
                                if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
                                {
                                    InternalClose();
                                    throw new TimeoutException(string.Format(
                                        "Closing handshake with remote [{0}] timeout [{1}].", RemoteEndPoint, ConnectTimeout));
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ShouldThrow(ex))
                                throw;
                        }
                        return;
                    }
                case _connecting:
                case _closing:
                    {
                        InternalClose();
                        return;
                    }
                case _disposed:
                case _none:
                default:
                    return;
            }
        }

        private void InternalClose()
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
                    _tcpClient.Close();
                    _tcpClient = null;
                }
            }
            catch (Exception) { }

            if (_receiveBuffer != null)
                _bufferManager.ReturnBuffer(_receiveBuffer);
            _receiveBufferOffset = 0;

            _log(string.Format("Disconnected from server [{0}] on [{1}].",
                this.RemoteEndPoint, DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff")));
            try
            {
                RaiseServerDisconnected();
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        public void Abort()
        {
            InternalClose();
        }

        private void StartClosingTimer()
        {
            // In abnormal cases (such as not having received a TCP Close 
            // from the server after a reasonable amount of time) a client MAY initiate the TCP Close.
            _closingTimeoutTimer.Change((int)CloseTimeout.TotalMilliseconds, Timeout.Infinite);
        }

        private void OnCloseTimeout()
        {
            // After both sending and receiving a Close message, an endpoint
            // considers the WebSocket connection closed and MUST close the
            // underlying TCP connection.  The server MUST close the underlying TCP
            // connection immediately; the client SHOULD wait for the server to
            // close the connection but MAY close the connection at any time after
            // sending and receiving a Close message, e.g., if it has not received a
            // TCP Close from the server in a reasonable time period.
            _log(string.Format("Closing timer timeout [{0}] then close automatically.", CloseTimeout));
            InternalClose();
        }

        #endregion

        #region Exception Handler

        private bool CloseIfShould(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException
                )
            {
                _log(ex.Message);

                Abort();

                return true;
            }

            return false;
        }

        private bool ShouldThrow(Exception ex)
        {
            if (ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut)
            {
                _log(ex.Message);
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
                    _log(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message));

                return false;
            }

            _log(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message));
            return true;
        }

        private void HandleUserSideError(Exception ex)
        {
            _log(string.Format("Client [{0}] error occurred in user side [{1}].", this, ex.Message));
        }

        #endregion

        #region Send

        public void SendText(string text)
        {
            SendFrame(new TextFrame(text).ToArray(_frameBuilder));
        }

        public void SendBinary(byte[] data)
        {
            SendBinary(data, 0, data.Length);
        }

        public void SendBinary(byte[] data, int offset, int count)
        {
            SendFrame(new BinaryFrame(data, offset, count).ToArray(_frameBuilder));
        }

        public void SendBinary(ArraySegment<byte> segment)
        {
            SendFrame(new BinaryFrame(segment).ToArray(_frameBuilder));
        }

        private void SendFrame(byte[] frame)
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
                    _stream.BeginWrite(frame, 0, frame.Length, HandleDataWritten, _stream);
                }
            }
            catch (Exception ex)
            {
                if (!CloseIfShould(ex))
                    throw;
            }
        }

        private void HandleDataWritten(IAsyncResult ar)
        {
            try
            {
                _stream.EndWrite(ar);
                _keepAliveTracker.OnDataSent();
            }
            catch (Exception ex)
            {
                if (!CloseIfShould(ex))
                    throw;
            }
        }

        #endregion

        #region Events

        public event EventHandler<WebSocketServerConnectedEventArgs> ServerConnected;
        public event EventHandler<WebSocketServerDisconnectedEventArgs> ServerDisconnected;
        public event EventHandler<WebSocketServerTextReceivedEventArgs> ServerTextReceived;
        public event EventHandler<WebSocketServerBinaryReceivedEventArgs> ServerBinaryReceived;

        private void RaiseServerConnected()
        {
            if (ServerConnected != null)
            {
                ServerConnected(this, new WebSocketServerConnectedEventArgs(this.RemoteEndPoint, this.LocalEndPoint));
            }
        }

        private void RaiseServerDisconnected()
        {
            if (ServerDisconnected != null)
            {
                ServerDisconnected(this, new WebSocketServerDisconnectedEventArgs(_remoteEndPoint, _localEndPoint));
            }
        }

        private void RaiseServerTextReceived(string text)
        {
            if (ServerTextReceived != null)
            {
                ServerTextReceived(this, new WebSocketServerTextReceivedEventArgs(this, text));
            }
        }

        private void RaiseServerBinaryReceived(byte[] data, int dataOffset, int dataLength)
        {
            if (ServerBinaryReceived != null)
            {
                ServerBinaryReceived(this, new WebSocketServerBinaryReceivedEventArgs(this, data, dataOffset, dataLength));
            }
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

        private void OnKeepAliveTimeout()
        {
            _log(string.Format("Keep-alive timer timeout [{1}].", KeepAliveTimeout));
            Close(WebSocketCloseCode.AbnormalClosure, "Keep-Alive Timeout");
        }

        private void OnKeepAlive()
        {
            if (_keepAliveLocker.WaitOne(0))
            {
                try
                {
                    if (State != WebSocketState.Open)
                        return;

                    if (_keepAliveTracker.ShouldSendKeepAlive())
                    {
                        var keepAliveFrame = new PingFrame().ToArray(_frameBuilder);
                        SendFrame(keepAliveFrame);
                        StartKeepAliveTimeoutTimer();

                        _keepAliveTracker.ResetTimer();
                    }
                }
                catch (Exception ex)
                {
                    _log(ex.Message);
                    Close(WebSocketCloseCode.EndpointUnavailable);
                }
                finally
                {
                    _keepAliveLocker.Release();
                }
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    InternalClose();
                }
                catch (Exception ex)
                {
                    _log(ex.Message);
                }
            }
        }

        #endregion
    }
}
