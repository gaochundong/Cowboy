using System;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.WebSockets.TestAsyncWebSocketClient
{
    class Program
    {
        static AsyncWebSocketClient _client;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            Task.Run(async () =>
            {
                try
                {
                    var config = new AsyncWebSocketClientConfiguration();
                    //config.SslTargetHost = "Cowboy";
                    //config.SslClientCertificates.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.cer"));
                    //config.SslPolicyErrorsBypassed = true;

                    var uri = new Uri("ws://echo.websocket.org/");
                    //var uri = new Uri("wss://127.0.0.1:22222/test");
                    //var uri = new Uri("ws://127.0.0.1:22222/test");
                    _client = new AsyncWebSocketClient(uri,
                        OnServerTextReceived,
                        OnServerBinaryReceived,
                        OnServerConnected,
                        OnServerDisconnected,
                        config);
                    await _client.Connect();

                    Console.WriteLine("WebSocket client has connected to server [{0}].", uri);
                    Console.WriteLine("Type something to send to server...");
                    while (_client.State == WebSocketState.Open)
                    {
                        try
                        {
                            string text = Console.ReadLine();
                            if (text == "quit")
                                break;
                            Task.Run(async () =>
                            {
                                //await _client.SendText(text);
                                //Console.WriteLine("Client [{0}] send text -> [{1}].", _client.LocalEndPoint, text);
                                await _client.SendBinary(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Client [{0}] send binary -> [{1}].", _client.LocalEndPoint, text);
                            }).Forget();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    await _client.Close(WebSocketCloseCode.NormalClosure);
                    Console.WriteLine("WebSocket client has disconnected from server [{0}].", uri);
                }
                catch (Exception ex)
                {
                    Logger.Get<Program>().Error(ex.Message, ex);
                }
            }).Wait();

            Console.ReadKey();
        }

        private static async Task OnServerConnected(AsyncWebSocketClient client)
        {
            Console.WriteLine(string.Format("WebSocket server [{0}] has connected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }

        private static async Task OnServerTextReceived(AsyncWebSocketClient client, string text)
        {
            Console.Write(string.Format("WebSocket server [{0}] received Text --> ", client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await Task.CompletedTask;
        }

        private static async Task OnServerBinaryReceived(AsyncWebSocketClient client, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("WebSocket server [{0}] received Binary --> ", client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await Task.CompletedTask;
        }

        private static async Task OnServerDisconnected(AsyncWebSocketClient client)
        {
            Console.WriteLine(string.Format("WebSocket server [{0}] has disconnected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }
    }
}
