using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace Cowboy.TcpLika
{
    internal class TcpLikaCommandLineOptions
    {
        public TcpLikaCommandLineOptions()
        {
            this.RemoteEndPoints = new List<IPEndPoint>();
            this.SslClientCertificates = new X509CertificateCollection();
        }

        public List<IPEndPoint> RemoteEndPoints { get; set; }    // Remote addresses to connect

        public bool IsSetThreads { get; set; }
        public int Threads { get; set; }                 // Number of parallel threads to use

        public bool IsSetNagle { get; set; }
        public bool Nagle { get; set; }                  // on|off, Control Nagle algorithm (set TCP_NODELAY)
        public bool IsSetReceiveBufferSize { get; set; }
        public int ReceiveBufferSize { get; set; }       // Set TCP receive buffers (set SO_RCVBUF)
        public bool IsSetSendBufferSize { get; set; }
        public int SendBufferSize { get; set; }          // Set TCP send buffers (set SO_SNDBUF)

        public bool IsSetConnections { get; set; }
        public int Connections { get; set; }             // Connections to keep open to the destinations
        public bool IsSetChannelLifetime { get; set; }
        public TimeSpan ChannelLifetime { get; set; }    // Shut down each connection after time milliseconds

        public bool IsSetWebSocket { get; set; }         // Use RFC6455 WebSocket transport
        public bool IsSetWebSocketPath { get; set; }
        public string WebSocketPath { get; set; }
        public bool IsSetWebSocketProtocol { get; set; }
        public string WebSocketProtocol { get; set; }

        public bool IsSetSsl { get; set; }
        public bool IsSetSslTargetHost { get; set; }
        public string SslTargetHost { get; set; }
        public bool IsSetSslClientCertificateFilePath { get; set; }
        public string SslClientCertificateFilePath { get; set; }
        public X509CertificateCollection SslClientCertificates { get; set; }
        public bool IsSetSslPolicyErrorsBypassed { get; set; }

        public bool IsSetHelp { get; set; }
        public bool IsSetVersion { get; set; }
    }
}
