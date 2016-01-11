using System;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Cowboy.Sockets
{
    public sealed class AsyncTcpSocketClientConfiguration
    {
        public AsyncTcpSocketClientConfiguration()
        {
            Framing = true;
            Masking = false;

            InitialBufferAllocationCount = 4;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            NoDelay = true;
            LingerState = new LingerOption(false, 0);

            SslEnabled = false;
            SslTargetHost = null;
            SslClientCertificates = new X509CertificateCollection();
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;

            ConnectTimeout = TimeSpan.FromSeconds(15);
        }

        public bool Framing { get; set; }
        public bool Masking { get; set; }

        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }

        public bool SslEnabled { get; set; }
        public string SslTargetHost { get; set; }
        public X509CertificateCollection SslClientCertificates { get; set; }
        public EncryptionPolicy SslEncryptionPolicy { get; set; }
        public SslProtocols SslEnabledProtocols { get; set; }
        public bool SslCheckCertificateRevocation { get; set; }
        public bool SslPolicyErrorsBypassed { get; set; }

        public TimeSpan ConnectTimeout { get; set; }
    }
}
