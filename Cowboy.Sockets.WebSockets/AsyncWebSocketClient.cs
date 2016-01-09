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
        private readonly SemaphoreSlim _opsLock = new SemaphoreSlim(1, 1);
        private readonly object _closeLock = new object();
        private bool _closed = false;
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
        private bool _isSecure = false;
        private string _secWebSocketKey;

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
            _isSecure = uri.Scheme.ToLowerInvariant() == "wss";

            this.ConnectTimeout = TimeSpan.FromSeconds(5);

            Initialize();
        }

        public AsyncWebSocketClient(Uri uri,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<AsyncWebSocketClient, Task> onServerConnected = null,
            Func<AsyncWebSocketClient, Task> onServerDisconnected = null,
            AsyncWebSocketClientConfiguration configuration = null)
            : this(uri, null,
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public AsyncWebSocketClient(Uri uri, string subProtocol,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<AsyncWebSocketClient, Task> onServerConnected = null,
            Func<AsyncWebSocketClient, Task> onServerDisconnected = null,
            AsyncWebSocketClientConfiguration configuration = null)
            : this(uri, subProtocol,
                 new InternalAsyncWebSocketClientMessageDispatcherImplementation(onServerDataReceived, onServerConnected, onServerDisconnected),
                 configuration)
        {
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);
        }

        #endregion

        #region Properties

        public TimeSpan ConnectTimeout { get; set; }
        public bool Connected { get { return _tcpClient != null && _tcpClient.Client.Connected; } }
        public IPEndPoint RemoteEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.LocalEndPoint : null; } }

        public string SubProtocol { get; private set; }
        public string Version { get { return "13"; } }
        public string Extensions { get; set; }
        public string Origin { get; set; }
        public Dictionary<string, string> Cookies { get; set; }

        public Uri Uri { get { return _uri; } }
        public bool Handshaked { get; private set; }
        //public WebSocketCloseStatus? CloseStatus;
        //public string CloseStatusDescription;
        //public WebSocketState State;

        #endregion

        #region Connect

        public async Task Connect()
        {
            if (await _opsLock.WaitAsync(0))
            {
                try
                {
                    if (!Connected)
                    {
                        _closed = false;

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

                        var handshaker = Handshake();
                        if (!handshaker.Wait(ConnectTimeout))
                        {
                            throw new TimeoutException(string.Format(
                                "Handshake with remote [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
                        }
                        if (!handshaker.Result)
                        {
                            throw new WebSocketException(string.Format(
                                "Handshake with remote [{0}] failed due to mismatched security key.", _remoteEndPoint));
                        }

                        _log.DebugFormat("Connected to server [{0}] with dispatcher [{1}] on [{2}].",
                            this.RemoteEndPoint,
                            _dispatcher.GetType().Name,
                            DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));
                        await _dispatcher.OnServerConnected(this);

                        Task.Run(async () =>
                        {
                            await Process();
                        })
                        .Forget();
                    }
                }
                catch (Exception ex)
                when (ex is TimeoutException || ex is WebSocketException)
                {
                    _log.Error(ex.Message, ex);
                    await Close();
                    throw;
                }
                finally
                {
                    _opsLock.Release();
                }
            }
        }

        public async Task Close()
        {
            if (Monitor.TryEnter(_closeLock))
            {
                try
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
                                _tcpClient = null;
                            }
                        }
                        catch (Exception ex)
                        {
                            _log.Error(ex.Message, ex);
                        }

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
                }
                finally
                {
                    Monitor.Exit(_closeLock);
                }
            }
        }

        private async Task Process()
        {
            try
            {
                while (Connected)
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    if (receiveCount == 0)
                        break;

                    BufferDeflector.AppendBuffer(_bufferManager, ref _receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                    while (true)
                    {
                        //var frameHeader = ReadFrameHeader();
                        //if (TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize <= _sessionBufferCount)
                        //{
                        //    await _dispatcher.OnServerDataReceived(this, _sessionBuffer, TcpFrameHeader.HEADER_SIZE, frameHeader.PayloadSize);
                        //    BufferDeflector.ShiftBuffer(_bufferManager, TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize, ref _sessionBuffer, ref _sessionBufferCount);
                        //}
                        //else
                        //{
                        //    break;
                        //}
                    }
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            finally
            {
                await Close();
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
            if (!_isSecure)
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

        private async Task<bool> Handshake()
        {
            var requestBuffer = WebSocketClientHandshaker.CreateOpenningHandshakeRequest(
                this.Uri.Host,
                this.Uri.PathAndQuery,
                out _secWebSocketKey,
                this.SubProtocol,
                this.Version,
                this.Extensions,
                this.Origin,
                this.Cookies);

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

            var result = WebSocketClientHandshaker.VerifyOpenningHandshakeResponse(_sessionBuffer, 0, terminatorIndex + HeaderTerminator.Length, _secWebSocketKey);

            BufferDeflector.ShiftBuffer(_bufferManager, terminatorIndex + HeaderTerminator.Length, ref _sessionBuffer, ref _sessionBufferCount);

            return result;
        }

        private bool FindHeaderTerminator(out int index)
        {
            index = -1;

            for (int i = 0; i < _sessionBufferCount; i++)
            {
                if (i + HeaderTerminator.Length < _sessionBufferCount)
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
                throw new InvalidProgramException("This client has not connected to server.");
            }

            try
            {
                if (_stream.CanWrite)
                {
                    await _stream.WriteAsync(data, offset, count);
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        #endregion
    }
}
