using System;
using System.Collections.Generic;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using Cowboy.WebSockets.Extensions;
using Cowboy.WebSockets.SubProtocols;

namespace Cowboy.WebSockets
{
    public sealed class AsyncWebSocketClientConfiguration
    {
        public AsyncWebSocketClientConfiguration()
        {
            InitialPooledBufferCount = 4;
            ReceiveBufferSize = 8192;
            SendBufferSize = 8192;
            ReceiveTimeout = TimeSpan.Zero;
            SendTimeout = TimeSpan.Zero;
            NoDelay = true;
            LingerState = new LingerOption(false, 0); // The socket will linger for x seconds after Socket.Close is called.

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
            ReasonableFragmentSize = 4096;

            EnabledExtensions = new Dictionary<string, IWebSocketExtensionNegotiator>()
            {
                { PerMessageCompressionExtension.RegisteredToken, new PerMessageCompressionExtensionNegotiator() },
            };
            EnabledSubProtocols = new Dictionary<string, IWebSocketSubProtocolNegotiator>();

            OfferedExtensions = new List<WebSocketExtensionOfferDescription>()
            {
                new WebSocketExtensionOfferDescription(PerMessageCompressionExtension.RegisteredToken),
            };
            RequestedSubProtocols = new List<WebSocketSubProtocolRequestDescription>();
        }

        public int InitialPooledBufferCount { get; set; }
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
        public int ReasonableFragmentSize { get; set; }

        public Dictionary<string, IWebSocketExtensionNegotiator> EnabledExtensions { get; set; }
        public Dictionary<string, IWebSocketSubProtocolNegotiator> EnabledSubProtocols { get; set; }

        public List<WebSocketExtensionOfferDescription> OfferedExtensions { get; set; }
        public List<WebSocketSubProtocolRequestDescription> RequestedSubProtocols { get; set; }
    }
}
