using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;

namespace Cowboy.Sockets
{
    public sealed class AsyncTcpSocketServerConfiguration
    {
        public AsyncTcpSocketServerConfiguration()
        {
            InitialBufferAllocationCount = 100;
            ReceiveBufferSize = 64;
            PendingConnectionBacklog = 200;
            IsPackingEnabled = true;
            AllowNatTraversal = true;
            ExclusiveAddressUse = true;

            UseSsl = false;
            SslServerCertificate = null;
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslClientCertificateRequired = true;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;
        }

        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int PendingConnectionBacklog { get; set; }
        public bool IsPackingEnabled { get; set; }
        public bool AllowNatTraversal { get; set; }
        public bool ExclusiveAddressUse { get; set; }

        public bool UseSsl { get; set; }
        public X509Certificate2 SslServerCertificate { get; set; }
        public EncryptionPolicy SslEncryptionPolicy { get; set; }
        public SslProtocols SslEnabledProtocols { get; set; }
        public bool SslClientCertificateRequired { get; set; }
        public bool SslCheckCertificateRevocation { get; set; }
        public bool SslPolicyErrorsBypassed { get; set; }
    }
}
