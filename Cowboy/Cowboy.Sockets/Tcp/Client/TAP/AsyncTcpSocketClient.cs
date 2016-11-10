using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Logrila.Logging;

namespace Cowboy.Sockets
{
    public class AsyncTcpSocketClient
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<AsyncTcpSocketClient>();
        private TcpClient _tcpClient;
        private readonly IAsyncTcpSocketClientMessageDispatcher _dispatcher;
        private readonly AsyncTcpSocketClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private Stream _stream;
        private ArraySegment<byte> _receiveBuffer = default(ArraySegment<byte>);
        private int _receiveBufferOffset = 0;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _disposed = 5;

        #endregion

        #region Constructors

        public AsyncTcpSocketClient(IPAddress remoteAddress, int remotePort, IPAddress localAddress, int localPort, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), new IPEndPoint(localAddress, localPort), dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteAddress, int remotePort, IPEndPoint localEP, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), localEP, dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteAddress, int remotePort, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPEndPoint remoteEP, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
            : this(remoteEP, null, dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            _remoteEndPoint = remoteEP;
            _localEndPoint = localEP;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new AsyncTcpSocketClientConfiguration();

            if (_configuration.BufferManager == null)
                throw new InvalidProgramException("The buffer manager in configuration cannot be null.");
            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");
        }

        public AsyncTcpSocketClient(IPAddress remoteAddress, int remotePort, IPAddress localAddress, int localPort,
            Func<AsyncTcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<AsyncTcpSocketClient, Task> onServerConnected = null,
            Func<AsyncTcpSocketClient, Task> onServerDisconnected = null,
            AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), new IPEndPoint(localAddress, localPort),
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteAddress, int remotePort, IPEndPoint localEP,
            Func<AsyncTcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<AsyncTcpSocketClient, Task> onServerConnected = null,
            Func<AsyncTcpSocketClient, Task> onServerDisconnected = null,
            AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), localEP,
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteAddress, int remotePort,
            Func<AsyncTcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<AsyncTcpSocketClient, Task> onServerConnected = null,
            Func<AsyncTcpSocketClient, Task> onServerDisconnected = null,
            AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort),
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public AsyncTcpSocketClient(IPEndPoint remoteEP,
            Func<AsyncTcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<AsyncTcpSocketClient, Task> onServerConnected = null,
            Func<AsyncTcpSocketClient, Task> onServerDisconnected = null,
            AsyncTcpSocketClientConfiguration configuration = null)
            : this(remoteEP, null,
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public AsyncTcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP,
            Func<AsyncTcpSocketClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<AsyncTcpSocketClient, Task> onServerConnected = null,
            Func<AsyncTcpSocketClient, Task> onServerDisconnected = null,
            AsyncTcpSocketClientConfiguration configuration = null)
            : this(remoteEP, localEP,
                 new InternalAsyncTcpSocketClientMessageDispatcherImplementation(onServerDataReceived, onServerConnected, onServerDisconnected),
                 configuration)
        {
        }

        #endregion

        #region Properties

        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }

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

        public TcpSocketConnectionState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return TcpSocketConnectionState.None;
                    case _connecting:
                        return TcpSocketConnectionState.Connecting;
                    case _connected:
                        return TcpSocketConnectionState.Connected;
                    case _disposed:
                        return TcpSocketConnectionState.Closed;
                    default:
                        return TcpSocketConnectionState.Closed;
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
                throw new InvalidOperationException("This tcp socket client has already connected to server.");
            }

            try
            {
                Clean();

                _tcpClient = _localEndPoint != null ? new TcpClient(_localEndPoint) : new TcpClient(_remoteEndPoint.Address.AddressFamily);

                var awaiter = _tcpClient.ConnectAsync(_remoteEndPoint.Address, _remoteEndPoint.Port);
                if (!awaiter.Wait(ConnectTimeout))
                {
                    await Close(false);
                    throw new TimeoutException(string.Format(
                        "Connect to [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
                }

                ConfigureClient();
                var negotiator = NegotiateStream(_tcpClient.GetStream());
                if (!negotiator.Wait(ConnectTimeout))
                {
                    await Close(false);
                    throw new TimeoutException(string.Format(
                        "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
                }
                _stream = negotiator.Result;

                if (_receiveBuffer == default(ArraySegment<byte>))
                    _receiveBuffer = _configuration.BufferManager.BorrowBuffer();
                _receiveBufferOffset = 0;

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
                        await Process();
                    })
                    .Forget();
                }
                else
                {
                    await Close();
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
                throw;
            }
        }

        private async Task Process()
        {
            try
            {
                int frameLength;
                byte[] payload;
                int payloadOffset;
                int payloadCount;
                int consumedLength = 0;

                while (State == TcpSocketConnectionState.Connected)
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer.Array, _receiveBuffer.Offset + _receiveBufferOffset, _receiveBuffer.Count - _receiveBufferOffset);
                    if (receiveCount == 0)
                        break;

                    SegmentBufferDeflector.ReplaceBuffer(_configuration.BufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);
                    consumedLength = 0;

                    while (true)
                    {
                        frameLength = 0;
                        payload = null;
                        payloadOffset = 0;
                        payloadCount = 0;

                        if (_configuration.FrameBuilder.Decoder.TryDecodeFrame(_receiveBuffer.Array, _receiveBuffer.Offset + consumedLength, _receiveBufferOffset - consumedLength,
                            out frameLength, out payload, out payloadOffset, out payloadCount))
                        {
                            try
                            {
                                await _dispatcher.OnServerDataReceived(this, payload, payloadOffset, payloadCount);
                            }
                            catch (Exception ex)
                            {
                                HandleUserSideError(ex);
                            }
                            finally
                            {
                                consumedLength += frameLength;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    try
                    {
                        SegmentBufferDeflector.ShiftBuffer(_configuration.BufferManager, consumedLength, ref _receiveBuffer, ref _receiveBufferOffset);
                    }
                    catch (ArgumentOutOfRangeException) { }
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            finally
            {
                await Close();
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

            if (_configuration.SslClientCertificates == null || _configuration.SslClientCertificates.Count == 0)
            {
                await sslStream.AuthenticateAsClientAsync( // No client certificates are used in the authentication. The certificate revocation list is not checked during authentication.
                    _configuration.SslTargetHost); // The name of the server that will share this SslStream. The value specified for targetHost must match the name on the server's certificate.
            }
            else
            {
                await sslStream.AuthenticateAsClientAsync(
                    _configuration.SslTargetHost, // The name of the server that will share this SslStream. The value specified for targetHost must match the name on the server's certificate.
                    _configuration.SslClientCertificates, // The X509CertificateCollection that contains client certificates.
                    _configuration.SslEnabledProtocols, // The SslProtocols value that represents the protocol used for authentication.
                    _configuration.SslCheckCertificateRevocation); // A Boolean value that specifies whether the certificate revocation list is checked during authentication.
            }

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

        #endregion

        #region Close

        public async Task Close()
        {
            await Close(true);
        }

        private async Task Close(bool shallNotifyUserSide)
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            Clean();

            if (shallNotifyUserSide)
            {
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
        }

        private void Clean()
        {
            try
            {
                try
                {
                    if (_stream != null)
                    {
                        _stream.Dispose();
                    }
                }
                catch { }
                try
                {
                    if (_tcpClient != null)
                    {
                        _tcpClient.Dispose();
                    }
                }
                catch { }
            }
            catch { }
            finally
            {
                _stream = null;
                _tcpClient = null;
            }

            if (_receiveBuffer != default(ArraySegment<byte>))
                _configuration.BufferManager.ReturnBuffer(_receiveBuffer);
            _receiveBuffer = default(ArraySegment<byte>);
            _receiveBufferOffset = 0;
        }

        #endregion

        #region Exception Handler

        private bool IsSocketTimeOut(Exception ex)
        {
            return ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut;
        }

        private bool ShouldThrow(Exception ex)
        {
            if (IsSocketTimeOut(ex))
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

        private void HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Client [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
        }

        #endregion

        #region Send

        public async Task SendAsync(byte[] data)
        {
            await SendAsync(data, 0, data.Length);
        }

        public async Task SendAsync(byte[] data, int offset, int count)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidOperationException("This client has not connected to server.");
            }

            try
            {
                byte[] frameBuffer;
                int frameBufferOffset;
                int frameBufferLength;
                _configuration.FrameBuilder.Encoder.EncodeFrame(data, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

                await _stream.WriteAsync(frameBuffer, frameBufferOffset, frameBufferLength);
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        #endregion
    }
}
