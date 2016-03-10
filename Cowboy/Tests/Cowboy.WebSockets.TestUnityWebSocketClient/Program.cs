using System;
using System.Diagnostics;
using System.Text;

namespace Cowboy.WebSockets.TestUnityWebSocketClient
{
    class Program
    {
        static WebSocketClient _client;
        static Action<string> _log = (s) => Console.WriteLine(s);

        static void Main(string[] args)
        {
            try
            {
                var config = new WebSocketClientConfiguration();
                //config.SslTargetHost = "Cowboy";
                //config.SslClientCertificates.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.cer"));
                //config.SslPolicyErrorsBypassed = true;

                var uri = new Uri("ws://echo.websocket.org/");
                //var uri = new Uri("wss://127.0.0.1:22222/test");
                //var uri = new Uri("ws://127.0.0.1:22222/test");
                _client = new WebSocketClient(uri, config, _log);
                _client.ServerConnected += OnServerConnected;
                _client.ServerDisconnected += OnServerDisconnected;
                _client.ServerTextReceived += OnServerTextReceived;
                _client.ServerBinaryReceived += OnServerBinaryReceived;
                _client.Connect();

                Console.WriteLine("WebSocket client has connected to server [{0}].", uri);
                Console.WriteLine("Type something to send to server...");
                while (_client.State == WebSocketState.Open)
                {
                    try
                    {
                        string text = Console.ReadLine();
                        if (text == "quit")
                            break;

                        if (text == "many")
                        {
                            text = new string('x', 1024);
                            Stopwatch watch = Stopwatch.StartNew();
                            int count = 10000;
                            for (int i = 1; i <= count; i++)
                            {
                                _client.SendBinary(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Client [{0}] send binary -> Sequence[{1}] -> TextLength[{2}].",
                                    _client.LocalEndPoint, i, text.Length);
                            }
                            watch.Stop();
                            Console.WriteLine("Client [{0}] send binary -> Count[{1}] -> Cost[{2}] -> PerSecond[{3}].",
                                _client.LocalEndPoint, count, watch.ElapsedMilliseconds / 1000, count / (watch.ElapsedMilliseconds / 1000));
                        }
                        else if (text == "big")
                        {
                            text = new string('x', 1024 * 1024 * 100);
                            _client.SendBinary(Encoding.UTF8.GetBytes(text));
                            Console.WriteLine("Client [{0}] send binary -> [{1}].", _client.LocalEndPoint, text);
                        }
                        else
                        {
                            _client.SendBinary(Encoding.UTF8.GetBytes(text));
                            Console.WriteLine("Client [{0}] send binary -> [{1}].", _client.LocalEndPoint, text);

                            //_client.SendText(text);
                            //Console.WriteLine("Client [{0}] send text -> [{1}].", _client.LocalEndPoint, text);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                _client.Close(WebSocketCloseCode.NormalClosure);
                Console.WriteLine("WebSocket client has disconnected from server [{0}].", uri);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
            }

            Console.ReadKey();
        }

        private static void OnServerConnected(object sender, WebSocketServerConnectedEventArgs e)
        {
            Console.WriteLine(string.Format("WebSocket server [{0}] has connected.", e.RemoteEndPoint));
        }

        private static void OnServerDisconnected(object sender, WebSocketServerDisconnectedEventArgs e)
        {
            Console.WriteLine(string.Format("WebSocket server [{0}] has disconnected.", e.RemoteEndPoint));
        }

        private static void OnServerTextReceived(object sender, WebSocketServerTextReceivedEventArgs e)
        {
            Console.Write(string.Format("WebSocket server [{0}] received Text --> ", e.Client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", e.Text));
        }

        private static void OnServerBinaryReceived(object sender, WebSocketServerBinaryReceivedEventArgs e)
        {
            var text = Encoding.UTF8.GetString(e.Data, e.DataOffset, e.DataLength);
            Console.Write(string.Format("WebSocket server [{0}] received Binary --> ", e.Client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));
        }
    }
}
