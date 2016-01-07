using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public class AsyncTcpSocketClient
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<AsyncTcpSocketClient>();
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private readonly object _opsLock = new object();
        private bool _closed = false;
        private readonly IAsyncTcpSocketClientMessageDispatcher _dispatcher;
        private readonly AsyncTcpSocketClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private Stream _stream;
        private byte[] _receiveBuffer;
        private byte[] _sessionBuffer;
        private int _sessionBufferCount = 0;

        #endregion

        #region Constructors

        public AsyncTcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPAddress localIPAddress, int localPort, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), new IPEndPoint(localIPAddress, localPort), dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPEndPoint localEP, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), localEP, dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteIPAddress, int remotePort, IAsyncTcpSocketClientMessageDispatcher dispatcher, AsyncTcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), dispatcher, configuration)
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

            Initialize();
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);
        }

        #endregion

        #region Properties

        public bool Connected { get { return _tcpClient != null && _tcpClient.Client.Connected; } }
        public EndPoint RemoteEndPoint { get { return _tcpClient.Client.RemoteEndPoint; } }
        public EndPoint LocalEndPoint { get { return _tcpClient.Client.LocalEndPoint; } }

        #endregion

        #region Connect

        public void Connect()
        {
            lock (_opsLock)
            {
                if (!Connected)
                {
                    _closed = false;

                    Task.Run(async () =>
                    {
                        await Process();
                    });
                }
            }
        }

        private async Task Process()
        {
            if (_localEndPoint != null)
            {
                _tcpClient = new TcpClient(_localEndPoint);
            }
            else
            {
                _tcpClient = new TcpClient();
            }

            await _tcpClient.ConnectAsync(_remoteEndPoint.Address, _remoteEndPoint.Port);
            ConfigureClient();

            _log.DebugFormat("Connected to server [{0}] with dispatcher [{1}] on [{2}].",
                this.RemoteEndPoint, _dispatcher.GetType().Name, DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));

            _receiveBuffer = _bufferManager.BorrowBuffer();
            _sessionBuffer = _bufferManager.BorrowBuffer();
            _sessionBufferCount = 0;

            try
            {
                _stream = await NegotiateStream(_tcpClient.GetStream());

                await _dispatcher.OnServerConnected(this);

                while (Connected)
                {
                    int receiveCount = await _stream.ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    if (receiveCount == 0)
                        break;

                    if (!_configuration.Framing)
                    {
                        await _dispatcher.OnServerDataReceived(this, _receiveBuffer, 0, receiveCount);
                    }
                    else
                    {
                        BufferDeflector.AppendBuffer(_bufferManager, ref _receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                        while (true)
                        {
                            var frameHeader = TcpFrameHeader.ReadHeader(_sessionBuffer);
                            if (TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize <= _sessionBufferCount)
                            {
                                await _dispatcher.OnServerDataReceived(this, _sessionBuffer, TcpFrameHeader.HEADER_SIZE, frameHeader.PayloadSize);
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
                _bufferManager.ReturnBuffer(_receiveBuffer);
                _bufferManager.ReturnBuffer(_sessionBuffer);

                _log.DebugFormat("Disconnected from server [{0}] with dispatcher [{1}] on [{2}].",
                    this.RemoteEndPoint, _dispatcher.GetType().Name, DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));

                await Close();
            }
        }

        public async Task Close()
        {
            if (!_closed)
                await _dispatcher.OnServerDisconnected(this);

            lock (_opsLock)
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

        #endregion
    }
}
