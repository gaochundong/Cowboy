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

            Options.Add(TcpLikaOptionType.Help, HelpOptions);
            Options.Add(TcpLikaOptionType.Version, VersionOptions);
        }

        public static List<string> GetSingleOptions()
        {
            var singleOptionList = new List<string>();

            singleOptionList.AddRange(TcpLikaOptions.WebSocketOptions);

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
    --nagle, --no-delay
    {0}{0}ON|OFF, Control Nagle algorithm.
    --rcvbuf, --receive-buffer-size
    {0}{0}Set TCP receive buffer size.
    --sndbuf, --send-buffer-size
    {0}{0}Set TCP send buffer size.
    -c, --connections
    {0}{0}Connections to keep open to the destinations.
    -l, --connection-lifetime
    {0}{0}Shut down each connection after time milliseconds.
    -ws, --websocket
    {0}{0}Use RFC6455 WebSocket transport.
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
