using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

namespace Cowboy.TcpLika
{
    internal static class TcpLikaOptions
    {
        public static readonly ReadOnlyCollection<string> ThreadsOptions;
        public static readonly ReadOnlyCollection<string> NagleOptions;
        public static readonly ReadOnlyCollection<string> ReceiveBufferSizeOptions;
        public static readonly ReadOnlyCollection<string> SendBufferSizeOptions;
        public static readonly ReadOnlyCollection<string> ConnectionsOptions;
        public static readonly ReadOnlyCollection<string> ConnectionLifetimeOptions;
        public static readonly ReadOnlyCollection<string> WebSocketOptions;
        public static readonly ReadOnlyCollection<string> WebSocketPathOptions;
        public static readonly ReadOnlyCollection<string> WebSocketProtocolOptions;

        public static readonly ReadOnlyCollection<string> SslOptions;
        public static readonly ReadOnlyCollection<string> SslTargetHostOptions;
        public static readonly ReadOnlyCollection<string> SslClientCertificateFilePathOptions;
        public static readonly ReadOnlyCollection<string> SslBypassedErrorsOptions;

        public static readonly ReadOnlyCollection<string> HelpOptions;
        public static readonly ReadOnlyCollection<string> VersionOptions;

        public static readonly IDictionary<TcpLikaOptionType, ICollection<string>> Options;

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static TcpLikaOptions()
        {
            ThreadsOptions = new ReadOnlyCollection<string>(new string[] { "w", "workers", "t", "threads" });
            NagleOptions = new ReadOnlyCollection<string>(new string[] { "nagle", "no-delay" });
            ReceiveBufferSizeOptions = new ReadOnlyCollection<string>(new string[] { "rcvbuf", "receive-buffer-size" });
            SendBufferSizeOptions = new ReadOnlyCollection<string>(new string[] { "sndbuf", "send-buffer-size" });
            ConnectionsOptions = new ReadOnlyCollection<string>(new string[] { "c", "connections" });
            ConnectionLifetimeOptions = new ReadOnlyCollection<string>(new string[] { "l", "connection-lifetime" });
            WebSocketOptions = new ReadOnlyCollection<string>(new string[] { "ws", "websocket" });
            WebSocketPathOptions = new ReadOnlyCollection<string>(new string[] { "ws-path", "websocket-path" });
            WebSocketProtocolOptions = new ReadOnlyCollection<string>(new string[] { "ws-protocol", "websocket-protocol" });

            SslOptions = new ReadOnlyCollection<string>(new string[] { "ssl" });
            SslTargetHostOptions = new ReadOnlyCollection<string>(new string[] { "ssl-target-host" });
            SslClientCertificateFilePathOptions = new ReadOnlyCollection<string>(new string[] { "ssl-client-cert-file" });
            SslBypassedErrorsOptions = new ReadOnlyCollection<string>(new string[] { "ssl-bypass-errors" });

            HelpOptions = new ReadOnlyCollection<string>(new string[] { "h", "help" });
            VersionOptions = new ReadOnlyCollection<string>(new string[] { "v", "version" });

            Options = new Dictionary<TcpLikaOptionType, ICollection<string>>();

            Options.Add(TcpLikaOptionType.Threads, ThreadsOptions);
            Options.Add(TcpLikaOptionType.Nagle, NagleOptions);
            Options.Add(TcpLikaOptionType.ReceiveBufferSize, ReceiveBufferSizeOptions);
            Options.Add(TcpLikaOptionType.SendBufferSize, SendBufferSizeOptions);
            Options.Add(TcpLikaOptionType.Connections, ConnectionsOptions);
            Options.Add(TcpLikaOptionType.ConnectionLifetime, ConnectionLifetimeOptions);
            Options.Add(TcpLikaOptionType.WebSocket, WebSocketOptions);
            Options.Add(TcpLikaOptionType.WebSocketPath, WebSocketPathOptions);
            Options.Add(TcpLikaOptionType.WebSocketProtocol, WebSocketProtocolOptions);

            Options.Add(TcpLikaOptionType.Ssl, SslOptions);
            Options.Add(TcpLikaOptionType.SslTargetHost, SslTargetHostOptions);
            Options.Add(TcpLikaOptionType.SslClientCertificateFilePath, SslClientCertificateFilePathOptions);
            Options.Add(TcpLikaOptionType.SslBypassedErrors, SslBypassedErrorsOptions);

            Options.Add(TcpLikaOptionType.Help, HelpOptions);
            Options.Add(TcpLikaOptionType.Version, VersionOptions);
        }

        public static List<string> GetSingleOptions()
        {
            var singleOptionList = new List<string>();

            singleOptionList.AddRange(TcpLikaOptions.WebSocketOptions);

            singleOptionList.AddRange(TcpLikaOptions.SslOptions);
            singleOptionList.AddRange(TcpLikaOptions.SslBypassedErrorsOptions);

            singleOptionList.AddRange(TcpLikaOptions.HelpOptions);
            singleOptionList.AddRange(TcpLikaOptions.VersionOptions);

            return singleOptionList;
        }

        public static TcpLikaOptionType GetOptionType(string option)
        {
            var optionType = TcpLikaOptionType.None;

            foreach (var pair in Options)
            {
                foreach (var item in pair.Value)
                {
                    if (item == option)
                    {
                        optionType = pair.Key;
                        break;
                    }
                }
            }

            return optionType;
        }

        #region Usage

        public static readonly string Usage = string.Format(CultureInfo.CurrentCulture, @"
NAME

    tcplika - just a TCP testing tool

SYNOPSIS

    tcplika [OPTIONS] <host:port> [<host:port>...]

DESCRIPTION

    TcpLika is a TCP testing tool. 

OPTIONS

    -w, --workers, -t, --threads
    {0}{0}Number of parallel threads to use.
    -nagle, --no-delay
    {0}{0}ON|OFF, Control Nagle algorithm.
    -rcvbuf, --receive-buffer-size
    {0}{0}Set TCP receive buffer size.
    -sndbuf, --send-buffer-size
    {0}{0}Set TCP send buffer size.
    -c, --connections
    {0}{0}Connections to keep open to the destinations.
    -l, --connection-lifetime
    {0}{0}Shut down each connection after time milliseconds.
    -ws, --websocket
    {0}{0}Use RFC6455 WebSocket transport.
    -ws-path, --websocket-path
    {0}{0}Specify the WebSocket request-line path.
    -ws-protocol, --websocket-protocol
    {0}{0}Specify the WebSocket sub-protocol.
    -ssl
    {0}{0}Use Ssl/Tls channel.
    -ssl-target-host
    {0}{0}The name of the server that will share this Ssl stream.
    -ssl-client-cert-file
    {0}{0}The file that contains client certificates..
    -ssl-bypass-errors
    {0}{0}Bypass the ssl validation errors.
    -h, --help 
    {0}{0}Display this help and exit.
    -v, --version
    {0}{0}Output version information and exit.

EXAMPLES

    tcplika 127.0.0.1:9001
    Create 1 TCP connection to <127.0.0.1:9001>, then close immediately.

    tcplika 127.0.0.1:9001 -w 2 -c 10
    Create 10 TCP connections to <127.0.0.1:9001> in 2 threads parallel, 
    then close all connections immediately.

    tcplika 127.0.0.1:9001 -w 2 -c 10 -l 10000
    Create 10 TCP connections to <127.0.0.1:9001> in 2 threads parallel, 
    then close all connections after 10 seconds.

    tcplika 127.0.0.1:3202 -w 2 -c 10 -l 10000 -ws -ws-path 'test'
    Create 10 WebSocket connections to URI <ws://127.0.0.1:3202/test> 
    in 2 threads parallel, then close all connections after 10 seconds.

    tcplika 127.0.0.1:80 -w 8 -c 10000 -l 30000 -ws \
      -ssl -ssl-bypass-errors \
      -ssl-target-host 'WebSocketServer' \
      -ssl-client-cert-file 'D:\\Client.cer'
    Create 10000 WebSocket connections to <127.0.0.1:80> in 8 threads parallel,
    enable the SSL/TSL transport with the given host and certificate file,
    then close all connections after 30 seconds.

AUTHOR

    Written by Dennis Gao.

REPORTING BUGS

    Report bugs to <gaochundong@gmail.com>.

COPYRIGHT

    Copyright (C) 2015-2016 Dennis Gao. All Rights Reserved.
", @" ");

        #endregion
    }
}
