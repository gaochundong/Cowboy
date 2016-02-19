using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketServerConfiguration
    {
        public TcpSocketServerConfiguration()
        {
            InitialPooledBufferCount = 100;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            NoDelay = true;
            LingerState = new LingerOption(false, 0); // The socket will linger for x seconds after Socket.Close is called.

            PendingConnectionBacklog = 200;
            AllowNatTraversal = true;

            SslEnabled = false;
            SslServerCertificate = null;
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslClientCertificateRequired = true;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;

            ConnectTimeout = TimeSpan.FromSeconds(15);
            FrameBuilder = new SizePrefixedFrameBuilder();
        }

        public int InitialPooledBufferCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }

        public int PendingConnectionBacklog { get; set; }
        public bool AllowNatTraversal { get; set; }

        public bool SslEnabled { get; set; }
        public X509Certificate2 SslServerCertificate { get; set; }
        public EncryptionPolicy SslEncryptionPolicy { get; set; }
        public SslProtocols SslEnabledProtocols { get; set; }
        public bool SslClientCertificateRequired { get; set; }
        public bool SslCheckCertificateRevocation { get; set; }
        public bool SslPolicyErrorsBypassed { get; set; }

        public TimeSpan ConnectTimeout { get; set; }
        public IFrameBuilder FrameBuilder { get; set; }
    }
}
