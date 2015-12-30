using System;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Cowboy.Sockets
{
    public sealed class AsyncTcpSocketClientConfiguration
    {
        public AsyncTcpSocketClientConfiguration()
        {
            IsPackingEnabled = true;
            InitialBufferAllocationCount = 4;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            ExclusiveAddressUse = true;
            NoDelay = true;

            UseSsl = false;
            SslTargetHost = null;
            SslClientCertificates = new X509CertificateCollection();
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;
        }

        public bool IsPackingEnabled { get; set; }
        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool ExclusiveAddressUse { get; set; }
        public bool NoDelay { get; set; }

        public bool UseSsl { get; set; }
        public string SslTargetHost { get; set; }
        public X509CertificateCollection SslClientCertificates { get; set; }
        public EncryptionPolicy SslEncryptionPolicy { get; set; }
        public SslProtocols SslEnabledProtocols { get; set; }
        public bool SslCheckCertificateRevocation { get; set; }
        public bool SslPolicyErrorsBypassed { get; set; }
    }
}
