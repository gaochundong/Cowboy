using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Cowboy.WebSockets.Extensions;

namespace Cowboy.WebSockets
{
    public sealed class AsyncWebSocketClientConfiguration
    {
        public AsyncWebSocketClientConfiguration()
        {
            InitialBufferAllocationCount = 4;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            NoDelay = true;
            LingerState = new LingerOption(false, 0);

            SslTargetHost = null;
            SslClientCertificates = new X509CertificateCollection();
            SslEncryptionPolicy = EncryptionPolicy.RequireEncryption;
            SslEnabledProtocols = SslProtocols.Ssl3 | SslProtocols.Tls;
            SslCheckCertificateRevocation = false;
            SslPolicyErrorsBypassed = false;

            ConnectTimeout = TimeSpan.FromSeconds(10);
            CloseTimeout = TimeSpan.FromSeconds(5);
            KeepAliveInterval = TimeSpan.FromSeconds(30);
            KeepAliveTimeout = TimeSpan.FromSeconds(5);

            OfferedExtensions = new List<WebSocketExtensionOfferDescription>();
        }

        public int InitialBufferAllocationCount { get; set; }
        public int ReceiveBufferSize { get; set; }
        public int SendBufferSize { get; set; }
        public TimeSpan ReceiveTimeout { get; set; }
        public TimeSpan SendTimeout { get; set; }
        public bool NoDelay { get; set; }
        public LingerOption LingerState { get; set; }

        public string SslTargetHost { get; set; }
        public X509CertificateCollection SslClientCertificates { get; set; }
        public EncryptionPolicy SslEncryptionPolicy { get; set; }
        public SslProtocols SslEnabledProtocols { get; set; }
        public bool SslCheckCertificateRevocation { get; set; }
        public bool SslPolicyErrorsBypassed { get; set; }

        public TimeSpan ConnectTimeout { get; set; }
        public TimeSpan CloseTimeout { get; set; }
        public TimeSpan KeepAliveInterval { get; set; }
        public TimeSpan KeepAliveTimeout { get; set; }

        public List<WebSocketExtensionOfferDescription> OfferedExtensions { get; set; }
    }
}
