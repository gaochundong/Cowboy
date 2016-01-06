using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketSession
    {
        private static readonly ILog _log = Logger.Get<TcpSocketSession>();
        private readonly object _sync = new object();
        private readonly TcpClient _tcpClient;
        private readonly TcpSocketServerConfiguration _configuration;
        private readonly IBufferManager _bufferManager;
        private readonly TcpSocketServer _server;
        private readonly string _sessionKey;
        private Stream _stream;

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

            this.ReceiveBuffer = _bufferManager.BorrowBuffer();
            this.SessionBuffer = _bufferManager.BorrowBuffer();
            this.SessionBufferCount = 0;

            _sessionKey = Guid.NewGuid().ToString();

            ConfigureClient();

            _stream = NegotiateStream(_tcpClient.GetStream());
        }

        public string SessionKey { get { return _sessionKey; } }

        internal Stream Stream { get { return _stream; } }
        public EndPoint RemoteEndPoint { get { return _tcpClient.Client.RemoteEndPoint; } }
        public EndPoint LocalEndPoint { get { return _tcpClient.Client.LocalEndPoint; } }
        public bool Connected { get { return _tcpClient.Client.Connected; } }
        public TcpSocketServer Server { get { return _server; } }

        internal byte[] ReceiveBuffer { get; private set; }
        internal byte[] SessionBuffer { get; private set; }
        internal int SessionBufferCount { get; private set; }

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

            sslStream.AuthenticateAsServer(
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

        internal void AppendBuffer(int appendedCount)
        {
            if (appendedCount <= 0) return;

            lock (_sync)
            {
                if (this.SessionBuffer.Length < (this.SessionBufferCount + appendedCount))
                {
                    byte[] autoExpandedBuffer = _bufferManager.BorrowBuffer();
                    if (autoExpandedBuffer.Length < (this.SessionBufferCount + appendedCount) * 2)
                    {
                        _bufferManager.ReturnBuffer(autoExpandedBuffer);
                        autoExpandedBuffer = new byte[(this.SessionBufferCount + appendedCount) * 2];
                    }

                    Array.Copy(this.SessionBuffer, 0, autoExpandedBuffer, 0, this.SessionBufferCount);

                    var discardBuffer = this.SessionBuffer;
                    this.SessionBuffer = autoExpandedBuffer;
                    _bufferManager.ReturnBuffer(discardBuffer);
                }

                Array.Copy(this.ReceiveBuffer, 0, this.SessionBuffer, this.SessionBufferCount, appendedCount);
                this.SessionBufferCount = this.SessionBufferCount + appendedCount;
            }
        }

        internal void ShiftBuffer(int shiftStart)
        {
            lock (_sync)
            {
                if ((this.SessionBufferCount - shiftStart) < shiftStart)
                {
                    Array.Copy(this.SessionBuffer, shiftStart, this.SessionBuffer, 0, this.SessionBufferCount - shiftStart);
                    this.SessionBufferCount = this.SessionBufferCount - shiftStart;
                }
                else
                {
                    byte[] copyBuffer = _bufferManager.BorrowBuffer();
                    if (copyBuffer.Length < (this.SessionBufferCount - shiftStart))
                    {
                        _bufferManager.ReturnBuffer(copyBuffer);
                        copyBuffer = new byte[this.SessionBufferCount - shiftStart];
                    }

                    Array.Copy(this.SessionBuffer, shiftStart, copyBuffer, 0, this.SessionBufferCount - shiftStart);
                    Array.Copy(copyBuffer, 0, this.SessionBuffer, 0, this.SessionBufferCount - shiftStart);
                    this.SessionBufferCount = this.SessionBufferCount - shiftStart;

                    _bufferManager.ReturnBuffer(copyBuffer);
                }
            }
        }

        public void Close()
        {
            try
            {
                _tcpClient.Client.Disconnect(false);
            }
            finally
            {
                _bufferManager.ReturnBuffers(this.ReceiveBuffer, this.SessionBuffer);
            }
        }

        public override string ToString()
        {
            return _sessionKey;
        }
    }
}
