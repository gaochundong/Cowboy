using System;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.Sockets.WebSockets.TestAsyncWebSocketClient
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
                    //config.SslPolicyErrorsBypassed = false;

                    Uri uri = new Uri("ws://echo.websocket.org/");
                    _client = new AsyncWebSocketClient(uri,
                        OnServerTextReceived,
                        OnServerBinaryReceived,
                        OnServerConnected,
                        OnServerDisconnected,
                        config);
                    await _client.Connect();

                    Console.WriteLine("WebSocket client has connected to server [{0}].", uri);
                    Console.WriteLine("Type something to send to server...");
                    while (_client.Connected)
                    {
                        try
                        {
                            string text = Console.ReadLine();
                            if (text == "quit")
                                break;
                            Task.Run(async () =>
                            {
                                await _client.SendText(text);
                                Console.WriteLine("Client [{0}] send text -> [{1}].", _client.LocalEndPoint, text);
                            }).Forget();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                        }
                    }

                    await _client.Close();
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
            Console.WriteLine(string.Format("WebSocket server {0} has connected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }

        private static async Task OnServerTextReceived(AsyncWebSocketClient client, string text)
        {
            Console.Write(string.Format("WebSocket server {0} received Text --> ", client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await Task.CompletedTask;
        }

        private static async Task OnServerBinaryReceived(AsyncWebSocketClient client, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("WebSocket server {0} received Binary --> ", client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await Task.CompletedTask;
        }

        private static async Task OnServerDisconnected(AsyncWebSocketClient client)
        {
            Console.WriteLine(string.Format("WebSocket server {0} has disconnected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }
    }
}
