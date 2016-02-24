using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Cowboy.Logging;
using Cowboy.Sockets.Buffer;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketSession
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketSession>();
        private readonly object _opsLock = new object();
        private TcpClient _tcpClient;
        private readonly TcpSocketServerConfiguration _configuration;
        private readonly IBufferManager _bufferManager;
        private readonly TcpSocketServer _server;
        private readonly string _sessionKey;
        private Stream _stream;
        private byte[] _receiveBuffer;
        private int _receiveBufferOffset = 0;
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _localEndPoint;
        private bool _closed = false;

        #endregion

        #region Constructors

        public TcpSocketSession(
            TcpClient tcpClient,
            TcpSocketServerConfiguration configuration,
            IBufferManager bufferManager,
            TcpSocketServer server)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");
            if (server == null)
                throw new ArgumentNullException("server");

            _tcpClient = tcpClient;
            _configuration = configuration;
            _bufferManager = bufferManager;
            _server = server;

            _sessionKey = Guid.NewGuid().ToString();
            this.StartTime = DateTime.UtcNow;

            ConfigureClient();

            _remoteEndPoint = Active ? (IPEndPoint)_tcpClient.Client.RemoteEndPoint : null;
            _localEndPoint = Active ? (IPEndPoint)_tcpClient.Client.LocalEndPoint : null;
        }

        #endregion

        #region Properties

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get; private set; }
        public bool Active { get { return _tcpClient != null && _tcpClient.Connected; } }
        public IPEndPoint RemoteEndPoint { get { return Active ? (IPEndPoint)_tcpClient.Client.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return Active ? (IPEndPoint)_tcpClient.Client.LocalEndPoint : _localEndPoint; } }
        public TcpSocketServer Server { get { return _server; } }
        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }

        public override string ToString()
        {
            return string.Format("SessionKey[{0}], RemoteEndPoint[{1}], LocalEndPoint[{2}]",
                this.SessionKey, this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Connect

        internal void Start()
        {
            lock (_opsLock)
            {
                try
                {
                    if (Active)
                    {
                        _closed = false;

                        _stream = NegotiateStream(_tcpClient.GetStream());

                        _receiveBuffer = _bufferManager.BorrowBuffer();
                        _receiveBufferOffset = 0;

                        bool isErrorOccurredInUserSide = false;
                        try
                        {
                            _server.RaiseClientConnected(this);
                        }
                        catch (Exception ex)
                        {
                            isErrorOccurredInUserSide = true;
                            HandleUserSideError(ex);
                        }

                        if (!isErrorOccurredInUserSide)
                        {
                            ContinueReadBuffer();
                        }
                        else
                        {
                            Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message, ex);
                    Close();
                }
            }
        }

        public void Close()
        {
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
                            _tcpClient = null;
                        }
                    }
                    catch (Exception ex)
                    {
                        _log.Error(string.Format("Session [{0}] exception occurred, [{1}].", this, ex.Message), ex);
                    }
                    finally
                    {
                        _bufferManager.ReturnBuffer(_receiveBuffer);
                        _receiveBufferOffset = 0;
                    }

                    try
                    {
                        _server.RaiseClientDisconnected(this);
                    }
                    catch (Exception ex)
                    {
                        HandleUserSideError(ex);
                    }
                }
            }
        }

        private bool CloseIfShould(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException
                )
            {
                if (ex is SocketException)
                    _log.Error(string.Format("Session [{0}] exception occurred, [{1}].", this, ex.Message), ex);

                // connection has been closed
                Close();

                return true;
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

        private Stream NegotiateStream(Stream stream)
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
                        _log.ErrorFormat("Session [{0}] error occurred when validating remote certificate: [{1}], [{2}].",
                            this, this.RemoteEndPoint, sslPolicyErrors);

                    return false;
                });

            var sslStream = new SslStream(
                stream,
                false,
                validateRemoteCertificate,
                null,
                _configuration.SslEncryptionPolicy);

            var ar = sslStream.BeginAuthenticateAsServer(
                _configuration.SslServerCertificate, // The X509Certificate used to authenticate the server.
                _configuration.SslClientCertificateRequired, // A Boolean value that specifies whether the client must supply a certificate for authentication.
                _configuration.SslEnabledProtocols, // The SslProtocols value that represents the protocol used for authentication.
                _configuration.SslCheckCertificateRevocation, // A Boolean value that specifies whether the certificate revocation list is checked during authentication.
                null, _tcpClient);
            if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
            {
                Close();
                throw new TimeoutException(string.Format(
                    "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", this.RemoteEndPoint, ConnectTimeout));
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

        private void ContinueReadBuffer()
        {
            try
            {
                _stream.BeginRead(_receiveBuffer, _receiveBufferOffset, _receiveBuffer.Length - _receiveBufferOffset, HandleDataReceived, _stream);
            }
            catch (Exception ex)
            {
                if (!CloseIfShould(ex))
                    throw;
            }
        }

        private void HandleDataReceived(IAsyncResult ar)
        {
            if (!Active)
                return;

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
                    Close();
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
            // TCP guarantees delivery of all packets in the correct order. 
            // But there is no guarantee that one write operation on the sender-side will result in 
            // one read event on the receiving side. One call of write(message) by the sender 
            // can result in multiple messageReceived(session, message) events on the receiver; 
            // and multiple calls of write(message) can lead to a single messageReceived event.
            // In a stream-based transport such as TCP/IP, received data is stored into a socket receive buffer. 
            // Unfortunately, the buffer of a stream-based transport is not a queue of packets but a queue of bytes. 
            // It means, even if you sent two messages as two independent packets, 
            // an operating system will not treat them as two messages but as just a bunch of bytes. 
            // Therefore, there is no guarantee that what you read is exactly what your remote peer wrote.
            // There are three common techniques for splitting the stream of bytes into messages:
            //   1. use fixed length messages
            //   2. use a fixed length header that indicates the length of the body
            //   3. using a delimiter; for example many text-based protocols append
            //      a newline (or CR LF pair) after every message.
            int frameLength;
            byte[] payload;
            int payloadOffset;
            int payloadCount;

            BufferDeflector.ReplaceBuffer(_bufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);

            while (true)
            {
                if (_configuration.FrameBuilder.TryDecodeFrame(_receiveBuffer, _receiveBufferOffset,
                    out frameLength, out payload, out payloadOffset, out payloadCount))
                {
                    try
                    {
                        _server.RaiseClientDataReceived(this, payload, payloadOffset, payloadCount);
                    }
                    catch (Exception ex)
                    {
                        HandleUserSideError(ex);
                    }
                    finally
                    {
                        try
                        {
                            BufferDeflector.ShiftBuffer(_bufferManager, frameLength, ref _receiveBuffer, ref _receiveBufferOffset);
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

        private void HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Session [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
        }

        #endregion

        #region Send

        public void Send(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            Send(data, 0, data.Length);
        }

        public void Send(byte[] data, int offset, int count)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (!Active)
            {
                throw new InvalidProgramException("This session has been closed.");
            }

            try
            {
                if (_stream.CanWrite)
                {
                    var frame = _configuration.FrameBuilder.EncodeFrame(data, offset, count);
                    _stream.Write(frame, 0, frame.Length);
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException
                    && ex.InnerException != null
                    && ex.InnerException is SocketException
                    && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut)
                {
                    _log.Error(ex.Message, ex);
                }
                else
                {
                    if (!CloseIfShould(ex))
                        throw;
                }
            }
        }

        public void SendAsync(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            SendAsync(data, 0, data.Length);
        }

        public void SendAsync(byte[] data, int offset, int count)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (!Active)
            {
                throw new InvalidProgramException("This session has been closed.");
            }

            try
            {
                if (_stream.CanWrite)
                {
                    var frame = _configuration.FrameBuilder.EncodeFrame(data, offset, count);
                    _stream.BeginWrite(frame, 0, frame.Length, HandleDataWritten, _tcpClient);
                }
            }
            catch (Exception ex)
            {
                if (ex is IOException
                    && ex.InnerException != null
                    && ex.InnerException is SocketException
                    && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut)
                {
                    _log.Error(ex.Message, ex);
                }
                else
                {
                    if (!CloseIfShould(ex))
                        throw;
                }
            }
        }

        private void HandleDataWritten(IAsyncResult ar)
        {
            try
            {
                _stream.EndWrite(ar);
            }
            catch (Exception ex)
            {
                if (!CloseIfShould(ex))
                    throw;
            }
        }

        #endregion
    }
}
