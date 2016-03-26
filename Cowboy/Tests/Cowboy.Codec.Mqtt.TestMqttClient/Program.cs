using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Sockets;
using Logrila.Logging;
using Logrila.Logging.NLogIntegration;

namespace Cowboy.Codec.Mqtt.TestMqttClient
{
    class Program
    {
        static AsyncTcpSocketClient _client;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            try
            {
                var config = new AsyncTcpSocketClientConfiguration();

                config.FrameBuilder = new MqttPacketBuilder();

                var remoteEP = ResolveRemoteEndPoint(new Uri("http://test.mosquitto.org:1883/"));
                _client = new AsyncTcpSocketClient(remoteEP, new SimpleMessageDispatcher(), config);
                _client.Connect().Wait();

                Console.WriteLine("TCP client has connected to server [{0}].", remoteEP);
                Console.WriteLine("Type something to send to server...");
                while (true)
                {
                    try
                    {
                        string text = Console.ReadLine();
                        if (text == "quit")
                            break;
                        Task.Run(async () =>
                        {
                            if (text.ToUpperInvariant() == "CONNECT")
                            {
                                var connect = new CONNECT("Cowboy");
                                var bytes = connect.BuildPacketBytes();
                                await _client.SendAsync(bytes);
                                Console.WriteLine("Client [{0}] send CONNECT packet with length [{1}].", _client.LocalEndPoint, bytes.Length);
                            }
                        })
                        .Wait();
                    }
                    catch (Exception ex)
                    {
                        Logger.Get<Program>().Error(ex.Message, ex);
                    }
                }

                _client.Close().Wait();
                Console.WriteLine("TCP client has disconnected from server [{0}].", remoteEP);
            }
            catch (Exception ex)
            {
                Logger.Get<Program>().Error(ex.Message, ex);
            }

            Console.ReadKey();
        }

        private static IPEndPoint ResolveRemoteEndPoint(Uri uri)
        {
            var host = uri.Host;
            var port = uri.Port;

            IPAddress ipAddress;
            if (IPAddress.TryParse(host, out ipAddress))
            {
                return new IPEndPoint(ipAddress, port);
            }
            else
            {
                if (host.ToLowerInvariant() == "localhost")
                {
                    return new IPEndPoint(IPAddress.Parse(@"127.0.0.1"), port);
                }
                else
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    if (addresses.Length > 0)
                    {
                        return new IPEndPoint(addresses[0], port);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            string.Format("Cannot resolve host [{0}] by DNS.", host));
                    }
                }
            }
        }
    }
}
