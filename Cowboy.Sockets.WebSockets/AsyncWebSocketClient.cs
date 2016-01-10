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

        private static readonly byte[] HeaderTerminator = Encoding.UTF8.GetBytes("\r\n\r\n");
        private readonly string[] AllowedSchemes = new string[] { "ws", "wss" };
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

        #endregion

        #region Constructors

        public AsyncWebSocketClient(Uri uri, IAsyncWebSocketClientMessageDispatcher dispatcher, AsyncWebSocketClientConfiguration configuration = null)
            : this(uri, null, dispatcher, configuration)
        {
        }

        public AsyncWebSocketClient(Uri uri, string subProtocol, IAsyncWebSocketClientMessageDispatcher dispatcher, AsyncWebSocketClientConfiguration configuration = null)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");
            if (!AllowedSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                throw new NotSupportedException(
                    string.Format("Not support the specified scheme [{0}].", uri.Scheme));

            _uri = uri;
            SubProtocol = subProtocol;

            var host = _uri.Host;
            var port = _uri.Port > 0 ? _uri.Port : uri.Scheme.ToLowerInvariant() == "wss" ? 443 : 80;
            var path = _uri.PathAndQuery;

            IPAddress ipAddress;
            if (IPAddress.TryParse(host, out ipAddress))
            {
                _remoteEndPoint = new IPEndPoint(ipAddress, port);
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
            : this(uri, null,
                  onServerTextReceived, onServerBinaryReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public AsyncWebSocketClient(Uri uri, string subProtocol,
            Func<AsyncWebSocketClient, string, Task> onServerTextReceived = null,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerBinaryReceived = null,
            Func<AsyncWebSocketClient, Task> onServerConnected = null,
            Func<AsyncWebSocketClient, Task> onServerDisconnected = null,
            AsyncWebSocketClientConfiguration configuration = null)
            : this(uri, subProtocol,
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

        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }
        public TimeSpan KeepAliveInterval { get { return _configuration.KeepAliveInterval; } }

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
        public string SubProtocol { get; private set; }
        public string Version { get { return "13"; } }
        public string Extensions { get; set; }
        public string Origin { get; set; }
        public Dictionary<string, string> Cookies { get; set; }

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
                    throw new TimeoutException(string.Format(
                        "Connect to [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
                }

                ConfigureClient();
                var negotiator = NegotiateStream(_tcpClient.GetStream());
                if (!negotiator.Wait(ConnectTimeout))
                {
                    throw new TimeoutException(string.Format(
                        "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
                }
                _stream = negotiator.Result;

                _receiveBuffer = _bufferManager.BorrowBuffer();
                _sessionBuffer = _bufferManager.BorrowBuffer();
                _sessionBufferCount = 0;

                var handshaker = OpenHandshake();
                if (!handshaker.Wait(ConnectTimeout))
                {
                    throw new TimeoutException(string.Format(
                        "Handshake with remote [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
                }
                if (!handshaker.Result)
                {
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed.", _remoteEndPoint));
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
                await Close(WebSocketCloseStatus.EndpointUnavailable);
                throw;
            }
        }

        public async Task Close(WebSocketCloseStatus closeStatus)
        {
            await Close(closeStatus, null);
        }

        public async Task Close(WebSocketCloseStatus closeStatus, string closeStatusDescription)
        {
            if (State == WebSocketState.Closed || State == WebSocketState.None)
                return;

            var priorState = Interlocked.Exchange(ref _state, _closing);
            switch (priorState)
            {
                case _connected:
                    {
                        var closingHandshake = new CloseFrame(closeStatus, closeStatusDescription).ToArray();
                        try
                        {
                            if (_stream.CanWrite)
                            {
                                await _stream.WriteAsync(closingHandshake, 0, closingHandshake.Length);
#if DEBUG
                                _log.DebugFormat("Send client side close frame [{0}] [{1}].", closeStatus, closeStatusDescription);
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
                        var header = Frame.Decode(_sessionBuffer, _sessionBufferCount);
                        if (header != null && header.Length + header.PayloadLength <= _sessionBufferCount)
                        {
                            try
                            {
                                switch (header.OpCode)
                                {
                                    case FrameOpCode.Continuation:
                                        break;
                                    case FrameOpCode.Text:
                                        {
                                            var text = Encoding.UTF8.GetString(_sessionBuffer, header.Length, header.PayloadLength);
                                            await _dispatcher.OnServerTextReceived(this, text);
                                        }
                                        break;
                                    case FrameOpCode.Binary:
                                        {
                                            await _dispatcher.OnServerBinaryReceived(this, _sessionBuffer, header.Length, header.PayloadLength);
                                        }
                                        break;
                                    case FrameOpCode.Close:
                                        {
                                            var statusCode = _sessionBuffer[header.Length] * 256 + _sessionBuffer[header.Length + 1];
                                            var closeStatus = (WebSocketCloseStatus)statusCode;
                                            var closeStatusDescription = string.Empty;

                                            if (header.PayloadLength > 2)
                                            {
                                                closeStatusDescription = Encoding.UTF8.GetString(_sessionBuffer, header.Length + 2, header.PayloadLength - 2);
                                            }
#if DEBUG
                                            _log.DebugFormat("Receive server side close frame [{0}] [{1}].", closeStatus, closeStatusDescription);
#endif
                                            await Close(closeStatus, closeStatusDescription);
                                        }
                                        break;
                                    case FrameOpCode.Ping:
                                        {
                                            var ping = Encoding.UTF8.GetString(_sessionBuffer, header.Length, header.PayloadLength);
#if DEBUG
                                            _log.DebugFormat("Receive server side ping frame [{0}].", ping);
#endif
                                            var pong = new PongFrame(ping).ToArray();
                                            await SendFrame(pong);
#if DEBUG
                                            _log.DebugFormat("Send client side pong frame [{0}].", string.Empty);
#endif
                                        }
                                        break;
                                    case FrameOpCode.Pong:
                                        {
                                            var pong = Encoding.UTF8.GetString(_sessionBuffer, header.Length, header.PayloadLength);
#if DEBUG
                                            _log.DebugFormat("Receive server side pong frame [{0}].", pong);
#endif
                                        }
                                        break;
                                    default:
                                        {
                                            await Close(WebSocketCloseStatus.InvalidMessageType);
                                            throw new NotSupportedException(
                                                string.Format("Not support received opcode [{0}].", (byte)header.OpCode));
                                        }
                                }
                            }
                            catch (Exception ex)
                            {
                                _log.Error(ex.Message, ex);
                                await Close(WebSocketCloseStatus.AbnormalClosure);
                                throw;
                            }

                            BufferDeflector.ShiftBuffer(_bufferManager, header.Length + header.PayloadLength, ref _sessionBuffer, ref _sessionBufferCount);
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
                await Close(WebSocketCloseStatus.AbnormalClosure);
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
            var requestBuffer = WebSocketClientHandshaker.CreateOpenningHandshakeRequest(this, out _secWebSocketKey);

            await _stream.WriteAsync(requestBuffer, 0, requestBuffer.Length);

            int terminatorIndex = -1;
            while (!FindHeaderTerminator(out terminatorIndex))
            {
                int receiveCount = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                if (receiveCount == 0)
                {
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed due to receive zero bytes.", _remoteEndPoint));
                }

                BufferDeflector.AppendBuffer(_bufferManager, ref _receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                if (_sessionBufferCount > 2048)
                {
                    throw new WebSocketException(string.Format(
                        "Handshake with remote [{0}] failed due to receive weird stream.", _remoteEndPoint));
                }
            }

            var result = WebSocketClientHandshaker.VerifyOpenningHandshakeResponse(this, _sessionBuffer, 0, terminatorIndex + HeaderTerminator.Length, _secWebSocketKey);

            BufferDeflector.ShiftBuffer(_bufferManager, terminatorIndex + HeaderTerminator.Length, ref _sessionBuffer, ref _sessionBufferCount);

            return result;
        }

        private bool FindHeaderTerminator(out int index)
        {
            index = -1;

            for (int i = 0; i < _sessionBufferCount; i++)
            {
                if (i + HeaderTerminator.Length <= _sessionBufferCount)
                {
                    bool matched = true;
                    for (int j = 0; j < HeaderTerminator.Length; j++)
                    {
                        if (_sessionBuffer[i + j] != HeaderTerminator[j])
                        {
                            matched = false;
                            break;
                        }
                    }

                    if (matched)
                    {
                        index = i;
                        return true;
                    }
                }
                else
                {
                    break;
                }
            }

            return false;
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

        public async Task SendTextFragments(IEnumerable<string> fragments)
        {
            var frames = new TextFragmentation(fragments.ToList()).ToArrayList();
            foreach (var frame in frames)
            {
                await SendFrame(frame);
            }
        }

        public async Task SendBinaryFragments(IEnumerable<ArraySegment<byte>> fragments)
        {
            var frames = new BinaryFragmentation(fragments.ToList()).ToArrayList();
            foreach (var frame in frames)
            {
                await SendFrame(frame);
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
                    await Close(WebSocketCloseStatus.AbnormalClosure);
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
