using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;
using Cowboy.CommandLines;

namespace Cowboy.TcpLika
{
    public class TcpLikaCommandLine : CommandLine
    {
        private TcpLikaCommandLineOptions _options;

        public TcpLikaCommandLine(string[] args)
          : base(args)
        {
        }

        public override void Execute()
        {
            base.Execute();

            var singleOptions = TcpLikaOptions.GetSingleOptions();
            var getOptions = CommandLineParser.Parse(this.Arguments.ToArray<string>(), singleOptions.ToArray());
            _options = ParseOptions(getOptions);
            ValidateOptions(_options);

            if (_options.IsSetHelp)
            {
                RaiseCommandLineUsage(this, TcpLikaOptions.Usage);
            }
            else if (_options.IsSetVersion)
            {
                RaiseCommandLineUsage(this, this.Version);
            }
            else
            {
                StartEngine();
            }

            Terminate();
        }

        private void StartEngine()
        {
            try
            {
                var engine = new TcpLikaEngine(_options,
                    (string log)
                    =>
                        OutputText(
                            string.Format("{0}|MTID[{1}]|STID[{2}]|{3}",
                            DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fffffff"),
                            Thread.CurrentThread.ManagedThreadId,
                            Thread.CurrentThread.GetUnmanagedThreadId(),
                            log)));
                engine.Start();
            }
            catch (CommandLineException ex)
            {
                RaiseCommandLineException(this, ex);
            }
        }

        [SuppressMessage("Microsoft.Maintainability", "CA1502:AvoidExcessiveComplexity")]
        private static TcpLikaCommandLineOptions ParseOptions(CommandLineOptions commandLineOptions)
        {
            if (commandLineOptions == null)
                throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                  "Option used in invalid context -- {0}", "must specify a <host:port>."));

            var options = new TcpLikaCommandLineOptions();

            if (commandLineOptions.Arguments.Any())
            {
                foreach (var arg in commandLineOptions.Arguments.Keys)
                {
                    var optionType = TcpLikaOptions.GetOptionType(arg);
                    if (optionType == TcpLikaOptionType.None)
                        throw new CommandLineException(
                          string.Format(CultureInfo.CurrentCulture, "Option used in invalid context -- {0}",
                          string.Format(CultureInfo.CurrentCulture, "cannot parse the command line argument : [{0}].", arg)));

                    switch (optionType)
                    {
                        case TcpLikaOptionType.Threads:
                            {
                                options.IsSetThreads = true;
                                int threads;
                                if (!int.TryParse(commandLineOptions.Arguments[arg], out threads))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of threads option -- {0}.", commandLineOptions.Arguments[arg]));
                                if (threads < 1)
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of threads option -- {0}.", commandLineOptions.Arguments[arg]));
                                options.Threads = threads;
                            }
                            break;
                        case TcpLikaOptionType.Nagle:
                            {
                                options.IsSetNagle = true;
                                var nagle = commandLineOptions.Arguments[arg].ToString().ToUpperInvariant();
                                if (nagle != "ON" && nagle != "OFF")
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of nagle option (ON|OFF) -- {0}.", commandLineOptions.Arguments[arg]));
                                options.Nagle = nagle == "ON";
                            }
                            break;
                        case TcpLikaOptionType.ReceiveBufferSize:
                            {
                                options.IsSetReceiveBufferSize = true;
                                int bufferSize;
                                if (!int.TryParse(commandLineOptions.Arguments[arg], out bufferSize))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of receive buffer size option -- {0}.", commandLineOptions.Arguments[arg]));
                                if (bufferSize < 1)
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of receive buffer size option -- {0}.", commandLineOptions.Arguments[arg]));
                                options.ReceiveBufferSize = bufferSize;
                            }
                            break;
                        case TcpLikaOptionType.SendBufferSize:
                            {
                                options.IsSetSendBufferSize = true;
                                int bufferSize;
                                if (!int.TryParse(commandLineOptions.Arguments[arg], out bufferSize))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of send buffer size option -- {0}.", commandLineOptions.Arguments[arg]));
                                if (bufferSize < 1)
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of send buffer size option -- {0}.", commandLineOptions.Arguments[arg]));
                                options.SendBufferSize = bufferSize;
                            }
                            break;
                        case TcpLikaOptionType.Connections:
                            {
                                options.IsSetConnections = true;
                                int connections;
                                if (!int.TryParse(commandLineOptions.Arguments[arg], out connections))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of connections option -- {0}.", commandLineOptions.Arguments[arg]));
                                if (connections < 1)
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of connections option -- {0}.", commandLineOptions.Arguments[arg]));
                                options.Connections = connections;
                            }
                            break;
                        case TcpLikaOptionType.ConnectionLifetime:
                            {
                                options.IsSetChannelLifetime = true;
                                int milliseconds;
                                if (!int.TryParse(commandLineOptions.Arguments[arg], out milliseconds))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of channel lifetime [milliseconds] option -- {0}.", commandLineOptions.Arguments[arg]));
                                if (milliseconds < 1)
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid formats of channel lifetime [milliseconds] option -- {0}.", commandLineOptions.Arguments[arg]));
                                options.ChannelLifetime = TimeSpan.FromMilliseconds(milliseconds);
                            }
                            break;
                        case TcpLikaOptionType.WebSocket:
                            options.IsSetWebSocket = true;
                            break;
                        case TcpLikaOptionType.WebSocketPath:
                            {
                                options.IsSetWebSocketPath = true;
                                options.WebSocketPath = commandLineOptions.Arguments[arg].Trim();
                                if (string.IsNullOrEmpty(options.WebSocketPath))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid value of WebSocketPath option -- {0}.", commandLineOptions.Arguments[arg]));
                                options.WebSocketPath = "/" + options.WebSocketPath.TrimStart('/');
                            }
                            break;
                        case TcpLikaOptionType.WebSocketProtocol:
                            {
                                options.IsSetWebSocketProtocol = true;
                                options.WebSocketProtocol = commandLineOptions.Arguments[arg];
                                if (string.IsNullOrEmpty(options.WebSocketProtocol))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid value of WebSocketProtocol option -- {0}.", commandLineOptions.Arguments[arg]));
                            }
                            break;
                        case TcpLikaOptionType.Ssl:
                            options.IsSetSsl = true;
                            break;
                        case TcpLikaOptionType.SslTargetHost:
                            {
                                options.IsSetSslTargetHost = true;
                                options.SslTargetHost = commandLineOptions.Arguments[arg];
                                if (string.IsNullOrEmpty(options.SslTargetHost))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid value of SslTargetHost option -- {0}.", commandLineOptions.Arguments[arg]));
                            }
                            break;
                        case TcpLikaOptionType.SslClientCertificateFilePath:
                            {
                                options.IsSetSslClientCertificateFilePath = true;
                                options.SslClientCertificateFilePath = commandLineOptions.Arguments[arg];
                                if (string.IsNullOrEmpty(options.SslClientCertificateFilePath))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid value of SslClientCertificateFilePath option -- {0}.", commandLineOptions.Arguments[arg]));
                                if (!File.Exists(options.SslClientCertificateFilePath))
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid value of SslClientCertificateFilePath option -- {0} does not exist.", commandLineOptions.Arguments[arg]));

                                try
                                {
                                    options.SslClientCertificates.Add(new X509Certificate2(options.SslClientCertificateFilePath));
                                }
                                catch (CryptographicException ex)
                                {
                                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                        "Invalid value of SslClientCertificateFilePath option -- {0}.", ex.Message), ex);
                                }
                            }
                            break;
                        case TcpLikaOptionType.SslBypassedErrors:
                            options.IsSetSslPolicyErrorsBypassed = true;
                            break;
                        case TcpLikaOptionType.Help:
                            options.IsSetHelp = true;
                            break;
                        case TcpLikaOptionType.Version:
                            options.IsSetVersion = true;
                            break;
                    }
                }
            }

            if (commandLineOptions.Parameters.Any())
            {
                try
                {
                    foreach (var item in commandLineOptions.Parameters)
                    {
                        var splits = item.Split(':');
                        if (splits.Length < 2)
                            throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                                "{0} is not well formatted as <host:port>.", item));

                        var host = IPAddress.Parse(splits[0]);
                        var port = int.Parse(splits[1]);
                        var endpoint = new IPEndPoint(host, port);
                        options.RemoteEndPoints.Add(endpoint);
                    }
                }
                catch (Exception ex)
                {
                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                        "Invalid formats of endpoints -- {0}", ex.Message), ex);
                }
            }

            return options;
        }

        private static void ValidateOptions(TcpLikaCommandLineOptions options)
        {
            if (options.IsSetHelp || options.IsSetVersion)
                return;

            if (!options.RemoteEndPoints.Any())
            {
                throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                  "Option used in invalid context -- {0}", "must specify a <host:port>."));
            }

            if (options.IsSetSsl)
            {
                if (!options.IsSetSslTargetHost)
                {
                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                      "Option used in invalid context -- {0}", "must specify <SslTargetHost> when enable SSL/TLS."));
                }

                if (!options.IsSetSslClientCertificateFilePath)
                {
                    throw new CommandLineException(string.Format(CultureInfo.CurrentCulture,
                      "Option used in invalid context -- {0}", "must specify <SslClientCertificateFilePath> when enable SSL/TLS."));
                }
            }
        }
    }
}
