using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using Logrila.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestTcpMassiveConnection
{
    class Program
    {
        static void Main(string[] args)
        {
            NLogLogger.Use();

            Queue<TcpSocketClient> clients = new Queue<TcpSocketClient>();

            Console.WriteLine("TCP massive connections test.");
            while (true)
            {
                try
                {
                    Console.WriteLine("Type command to open or close connections to the server...");
                    string text = Console.ReadLine().ToLowerInvariant();
                    if (text == "quit")
                    {
                        break;
                    }
                    else if (text.StartsWith("open"))
                    {
                        var splitter = text.Split(' ');
                        if (splitter.Length > 1)
                        {
                            string input = splitter[1];
                            int count = 0;
                            if (int.TryParse(input, out count))
                            {
                                for (int i = 0; i < count; i++)
                                {
                                    var client = OpenTcpSocketClient();
                                    clients.Enqueue(client);
                                }

                                Console.WriteLine("Opened {0} TCP connections to the server, now {1} in total.",
                                    count, clients.Count);
                            }
                        }
                    }
                    else if (text.StartsWith("close"))
                    {
                        var splitter = text.Split(' ');
                        if (splitter.Length > 1)
                        {
                            string input = splitter[1];
                            int count = 0;
                            if (int.TryParse(input, out count))
                            {
                                int round = count;
                                while (clients.Count > 0 && round > 0)
                                {
                                    var client = clients.Dequeue();
                                    CloseTcpSocketClient(client);
                                    round--;
                                }

                                Console.WriteLine("Closed {0} TCP connections to the server, now {1} in total.",
                                    count - round, clients.Count);
                            }
                        }
                    }
                    else if (text.StartsWith("reconnect"))
                    {
                        var splitter = text.Split(' ');
                        if (splitter.Length > 1)
                        {
                            string input = splitter[1];
                            int count = 0;
                            if (int.TryParse(input, out count))
                            {
                                int round = count;

                                foreach (var client in clients)
                                {
                                    ReconnectTcpSocketClient(client);
                                    round--;
                                    if (round <= 0)
                                        break;
                                }

                                Console.WriteLine("Reconnected {0} TCP connections to the server, now {1} in total.",
                                    count - round, clients.Count);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        private static TcpSocketClient OpenTcpSocketClient()
        {
            var config = new TcpSocketClientConfiguration();
            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 65001);

            var client = new TcpSocketClient(remoteEP, config);
            client.ServerConnected += client_ServerConnected;
            client.ServerDisconnected += client_ServerDisconnected;
            client.ServerDataReceived += client_ServerDataReceived;
            client.Connect();

            return client;
        }

        private static void ReconnectTcpSocketClient(TcpSocketClient client)
        {
            client.Close();
            client.Connect();
        }

        private static void CloseTcpSocketClient(TcpSocketClient client)
        {
            client.ServerConnected -= client_ServerConnected;
            client.ServerDisconnected -= client_ServerDisconnected;
            client.ServerDataReceived -= client_ServerDataReceived;
            client.Close();
        }

        static void client_ServerConnected(object sender, TcpServerConnectedEventArgs e)
        {
            Console.WriteLine(string.Format("TCP server {0} has connected.", e.RemoteEndPoint));
        }

        static void client_ServerDisconnected(object sender, TcpServerDisconnectedEventArgs e)
        {
            Console.WriteLine(string.Format("TCP server {0} has disconnected.", e.RemoteEndPoint));
        }

        static void client_ServerDataReceived(object sender, TcpServerDataReceivedEventArgs e)
        {
            var text = Encoding.UTF8.GetString(e.Data, e.DataOffset, e.DataLength);
            Console.Write(string.Format("Server : {0} --> ", e.Client.RemoteEndPoint));
            if (e.DataLength < 1024 * 1024 * 1)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.WriteLine("{0} Bytes", e.DataLength);
            }
        }
    }
}
