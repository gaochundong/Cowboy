
namespace Cowboy.TcpLika
{
    internal enum TcpLikaOptionType
    {
        None = 0,

        RemoteEndPoints,    // Remote addresses to connect

        Threads,            // Number of parallel threads to use

        Nagle,              // on|off, Control Nagle algorithm (set TCP_NODELAY)
        ReceiveBufferSize,  // Set TCP receive buffers (set SO_RCVBUF)
        SendBufferSize,     // Set TCP send buffers (set SO_SNDBUF)

        Connections,        // Connections to keep open to the destinations
        ConnectionLifetime, // Shut down each connection after time milliseconds

        WebSocket,          // Use RFC6455 WebSocket transport
        WebSocketPath,
        WebSocketProtocol,

        Ssl,
        SslTargetHost,
        SslClientCertificateFilePath,
        SslBypassedErrors,

        Help,
        Version,
    }
}
