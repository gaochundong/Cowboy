using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Cowboy.Buffer;
using Logrila.Logging;

namespace Cowboy.Sockets
{
    public class TcpSocketClient : IDisposable
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketClient>();
        private TcpClient _tcpClient;
        private readonly TcpSocketClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private Stream _stream;
        private ArraySegment<byte> _receiveBuffer = default(ArraySegment<byte>);
        private int _receiveBufferOffset = 0;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _closed = 5;

        #endregion

        #region Constructors

        public TcpSocketClient(IPAddress remoteAddress, int remotePort, IPAddress localAddress, int localPort, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), new IPEndPoint(localAddress, localPort), configuration)
        {
        }

        public TcpSocketClient(IPAddress remoteAddress, int remotePort, IPEndPoint localEP = null, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), localEP, configuration)
        {
        }

        public TcpSocketClient(IPEndPoint remoteEP, TcpSocketClientConfiguration configuration = null)
            : this(remoteEP, null, configuration)
        {
        }

        public TcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP, TcpSocketClientConfiguration configuration = null)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");

            _remoteEndPoint = remoteEP;
            _localEndPoint = localEP;
            _configuration = configuration ?? new TcpSocketClientConfiguration();

            if (_configuration.BufferManager == null)
                throw new InvalidProgramException("The buffer manager in configuration cannot be null.");
            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");
        }

        #endregion

        #region Properties

        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }

        private bool Connected { get { return _tcpClient != null && _tcpClient.Client.Connected; } }
        public IPEndPoint RemoteEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return Connected ? (IPEndPoint)_tcpClient.Client.LocalEndPoint : _localEndPoint; } }

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
                    case _closed:
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

        public void Connect()
        {
            int origin = Interlocked.Exchange(ref _state, _connecting);
            if (!(origin == _none || origin == _closed))
            {
                Close(false);
                throw new InvalidOperationException("This tcp socket client is in invalid state when connecting.");
            }

            Clean();

            _tcpClient = _localEndPoint != null ?
                new TcpClient(_localEndPoint) :
                new TcpClient(_remoteEndPoint.Address.AddressFamily);
            SetSocketOptions();

            if (_receiveBuffer == default(ArraySegment<byte>))
                _receiveBuffer = _configuration.BufferManager.BorrowBuffer();
            _receiveBufferOffset = 0;

            var ar = _tcpClient.BeginConnect(_remoteEndPoint.Address, _remoteEndPoint.Port, null, _tcpClient);
            if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
            {
                Close(false);
                throw new TimeoutException(string.Format(
                    "Connect to [{0}] timeout [{1}].", _remoteEndPoint, ConnectTimeout));
            }
            _tcpClient.EndConnect(ar);

            if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
            {
                Close(false);
                throw new InvalidOperationException("This tcp socket client is in invalid state when connected.");
            }

            HandleTcpServerConnected();
        }

        public void Close()
        {
            Close(true);
        }

        private void Close(bool shallNotifyUserSide)
        {
            if (Interlocked.Exchange(ref _state, _closed) == _closed)
            {
                return;
            }

            Clean();

            if (shallNotifyUserSide)
            {
                try
                {
                    RaiseServerDisconnected();
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
                        _tcpClient.Close();
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

        #region Receive

        private void HandleTcpServerConnected()
        {
            try
            {
                _stream = NegotiateStream(_tcpClient.GetStream());

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
                    ContinueReadBuffer();
                }
                else
                {
                    Close();
                }
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
                Close();
            }
        }

        private void SetSocketOptions()
        {
            _tcpClient.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            _tcpClient.SendBufferSize = _configuration.SendBufferSize;
            _tcpClient.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
            _tcpClient.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
            _tcpClient.NoDelay = _configuration.NoDelay;
            _tcpClient.LingerState = _configuration.LingerState;

            if (_configuration.KeepAlive)
            {
                _tcpClient.Client.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.KeepAlive,
                    (int)_configuration.KeepAliveInterval.TotalMilliseconds);
            }

            _tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);
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

            IAsyncResult ar = null;
            if (_configuration.SslClientCertificates == null || _configuration.SslClientCertificates.Count == 0)
            {
                ar = sslStream.BeginAuthenticateAsClient( // No client certificates are used in the authentication. The certificate revocation list is not checked during authentication.
                    _configuration.SslTargetHost, // The name of the server that will share this SslStream. The value specified for targetHost must match the name on the server's certificate.
                    null, _tcpClient);
            }
            else
            {
                ar = sslStream.BeginAuthenticateAsClient(
                    _configuration.SslTargetHost, // The name of the server that will share this SslStream. The value specified for targetHost must match the name on the server's certificate.
                    _configuration.SslClientCertificates, // The X509CertificateCollection that contains client certificates.
                    _configuration.SslEnabledProtocols, // The SslProtocols value that represents the protocol used for authentication.
                    _configuration.SslCheckCertificateRevocation, // A Boolean value that specifies whether the certificate revocation list is checked during authentication.
                    null, _tcpClient);
            }
            if (!ar.AsyncWaitHandle.WaitOne(ConnectTimeout))
            {
                Close(false);
                throw new TimeoutException(string.Format(
                    "Negotiate SSL/TSL with remote [{0}] timeout [{1}].", this.RemoteEndPoint, ConnectTimeout));
            }
            sslStream.EndAuthenticateAsClient(ar);

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
                _stream.BeginRead(
                    _receiveBuffer.Array,
                    _receiveBuffer.Offset + _receiveBufferOffset,
                    _receiveBuffer.Count - _receiveBufferOffset,
                    HandleDataReceived,
                    _stream);
            }
            catch (Exception ex)
            {
                HandleReceiveOperationException(ex);
                throw;
            }
        }

        private void HandleDataReceived(IAsyncResult ar)
        {
            if (this.State != TcpSocketConnectionState.Connected)
                return;

            try
            {
                // when callback to here the stream may have been closed
                if (_stream == null)
                    return;

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
                    // the existing connection was forcibly closed by remote host
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
                HandleReceiveOperationException(ex);
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
            int consumedLength = 0;

            SegmentBufferDeflector.ReplaceBuffer(_configuration.BufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);

            while (true)
            {
                frameLength = 0;
                payload = null;
                payloadOffset = 0;
                payloadCount = 0;

                if (_configuration.FrameBuilder.Decoder.TryDecodeFrame(
                    _receiveBuffer.Array,
                    _receiveBuffer.Offset + consumedLength,
                    _receiveBufferOffset - consumedLength,
                    out frameLength, out payload, out payloadOffset, out payloadCount))
                {
                    try
                    {
                        RaiseServerDataReceived(payload, payloadOffset, payloadCount);
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

            SegmentBufferDeflector.ShiftBuffer(_configuration.BufferManager, consumedLength, ref _receiveBuffer, ref _receiveBufferOffset);
        }

        #endregion

        #region Exception Handler

        private void HandleSendOperationException(Exception ex)
        {
            if (IsSocketTimeOut(ex))
            {
                CloseIfShould(ex);
                throw new TcpSocketException(ex.Message, new TimeoutException(ex.Message, ex));
            }

            CloseIfShould(ex);
            throw new TcpSocketException(ex.Message, ex);
        }

        private void HandleReceiveOperationException(Exception ex)
        {
            if (IsSocketTimeOut(ex))
            {
                CloseIfShould(ex);
                throw new TcpSocketException(ex.Message, new TimeoutException(ex.Message, ex));
            }

            CloseIfShould(ex);
            throw new TcpSocketException(ex.Message, ex);
        }

        private bool IsSocketTimeOut(Exception ex)
        {
            return ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut;
        }

        private bool CloseIfShould(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException // buffer array operation
                || ex is ArgumentException      // buffer array operation
                )
            {
                _log.Error(ex.Message, ex);

                Close(); // intend to close the session

                return true;
            }

            return false;
        }

        private void HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Client [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
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

            if (this.State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidProgramException("This client has not connected to server.");
            }

            try
            {
                byte[] frameBuffer;
                int frameBufferOffset;
                int frameBufferLength;
                _configuration.FrameBuilder.Encoder.EncodeFrame(data, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

                _stream.Write(frameBuffer, frameBufferOffset, frameBufferLength);
            }
            catch (Exception ex)
            {
                HandleSendOperationException(ex);
                throw;
            }
        }

        public void BeginSend(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            BeginSend(data, 0, data.Length);
        }

        public void BeginSend(byte[] data, int offset, int count)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (this.State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidProgramException("This client has not connected to server.");
            }

            try
            {
                byte[] frameBuffer;
                int frameBufferOffset;
                int frameBufferLength;
                _configuration.FrameBuilder.Encoder.EncodeFrame(data, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

                _stream.BeginWrite(frameBuffer, frameBufferOffset, frameBufferLength, HandleDataWritten, _stream);
            }
            catch (Exception ex)
            {
                HandleSendOperationException(ex);
                throw;
            }
        }

        private void HandleDataWritten(IAsyncResult ar)
        {
            try
            {
                if (_stream != null)
                {
                    _stream.EndWrite(ar);
                }
            }
            catch (Exception ex)
            {
                HandleSendOperationException(ex);
                throw;
            }
        }

        public IAsyncResult BeginSend(byte[] data, AsyncCallback callback, object state)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            return BeginSend(data, 0, data.Length, callback, state);
        }

        public IAsyncResult BeginSend(byte[] data, int offset, int count, AsyncCallback callback, object state)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (this.State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidProgramException("This client has not connected to server.");
            }

            try
            {
                byte[] frameBuffer;
                int frameBufferOffset;
                int frameBufferLength;
                _configuration.FrameBuilder.Encoder.EncodeFrame(data, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

                return _stream.BeginWrite(frameBuffer, frameBufferOffset, frameBufferLength, callback, state);
            }
            catch (Exception ex)
            {
                HandleSendOperationException(ex);
                throw;
            }
        }

        public void EndSend(IAsyncResult asyncResult)
        {
            HandleDataWritten(asyncResult);
        }

        #endregion

        #region Events

        public event EventHandler<TcpServerConnectedEventArgs> ServerConnected;
        public event EventHandler<TcpServerDisconnectedEventArgs> ServerDisconnected;
        public event EventHandler<TcpServerDataReceivedEventArgs> ServerDataReceived;

        private void RaiseServerConnected()
        {
            if (ServerConnected != null)
            {
                ServerConnected(this, new TcpServerConnectedEventArgs(this.RemoteEndPoint, this.LocalEndPoint));
            }
        }

        private void RaiseServerDisconnected()
        {
            if (ServerDisconnected != null)
            {
                ServerDisconnected(this, new TcpServerDisconnectedEventArgs(_remoteEndPoint, _localEndPoint));
            }
        }

        private void RaiseServerDataReceived(byte[] data, int dataOffset, int dataLength)
        {
            if (ServerDataReceived != null)
            {
                ServerDataReceived(this, new TcpServerDataReceivedEventArgs(this, data, dataOffset, dataLength));
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Close();
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
