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
        public static readonly ReadOnlyCollection<string> ConnectRateOptions;
        public static readonly ReadOnlyCollection<string> ConnectTimeoutOptions;
        public static readonly ReadOnlyCollection<string> ChannelLifetimeOptions;
        public static readonly ReadOnlyCollection<string> WebSocketOptions;

        public static readonly ReadOnlyCollection<string> HelpOptions;
        public static readonly ReadOnlyCollection<string> VersionOptions;

        public static readonly IDictionary<TcpLikaOptionType, ICollection<string>> Options;

        [SuppressMessage("Microsoft.Performance", "CA1810:InitializeReferenceTypeStaticFieldsInline")]
        static TcpLikaOptions()
        {
            ThreadsOptions = new ReadOnlyCollection<string>(new string[] { "w", "workers", "threads" });
            NagleOptions = new ReadOnlyCollection<string>(new string[] { "nagle" });
            ReceiveBufferSizeOptions = new ReadOnlyCollection<string>(new string[] { "rcvbuf" });
            SendBufferSizeOptions = new ReadOnlyCollection<string>(new string[] { "sndbuf" });
            ConnectionsOptions = new ReadOnlyCollection<string>(new string[] { "c", "connections" });
            ConnectRateOptions = new ReadOnlyCollection<string>(new string[] { "connect-rate" });
            ConnectTimeoutOptions = new ReadOnlyCollection<string>(new string[] { "connect-timeout" });
            ChannelLifetimeOptions = new ReadOnlyCollection<string>(new string[] { "channel-lifetime" });
            WebSocketOptions = new ReadOnlyCollection<string>(new string[] { "ws", "websocket" });

            HelpOptions = new ReadOnlyCollection<string>(new string[] { "h", "help" });
            VersionOptions = new ReadOnlyCollection<string>(new string[] { "v", "version" });

            Options = new Dictionary<TcpLikaOptionType, ICollection<string>>();

            Options.Add(TcpLikaOptionType.Threads, ThreadsOptions);
            Options.Add(TcpLikaOptionType.Nagle, NagleOptions);
            Options.Add(TcpLikaOptionType.ReceiveBufferSize, ReceiveBufferSizeOptions);
            Options.Add(TcpLikaOptionType.SendBufferSize, SendBufferSizeOptions);
            Options.Add(TcpLikaOptionType.Connections, ConnectionsOptions);
            Options.Add(TcpLikaOptionType.ConnectRate, ConnectRateOptions);
            Options.Add(TcpLikaOptionType.ConnectTimeout, ConnectTimeoutOptions);
            Options.Add(TcpLikaOptionType.ChannelLifetime, ChannelLifetimeOptions);
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

    tcplika - just a tcp testing tool

SYNOPSIS

    tcplika [OPTIONS] <host:port> [<host:port>...]

DESCRIPTION

    TcpLika is a tcp testing tool. 

OPTIONS

    -d, --directory
    {0}{0}.
    -h, --help 
    {0}{0}Display this help and exit.
    -v, --version
    {0}{0}Output version information and exit.

EXAMPLES

    tcplika .
    .

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
