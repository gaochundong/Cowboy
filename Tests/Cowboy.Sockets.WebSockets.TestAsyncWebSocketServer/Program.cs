using System;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.Sockets.WebSockets.TestAsyncWebSocketServer
{
    class Program
    {
        static AsyncWebSocketServer _server;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            try
            {
                var catalog = new AsyncWebSocketServerModuleCatalog();
                catalog.RegisterModule(new TestWebSocketModule());

                var config = new AsyncWebSocketServerConfiguration();
                //config.SslServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.pfx", "Cowboy");
                //config.SslPolicyErrorsBypassed = false;

                _server = new AsyncWebSocketServer(22222, catalog);
                _server.Start();

                Console.WriteLine("WebSocket server has been started on [{0}].", _server.ListenedEndPoint);
                Console.WriteLine("Type something to send to clients...");
                while (true)
                {
                    try
                    {
                        string text = Console.ReadLine();
                        if (text == "quit")
                            break;
                        Task.Run(async () =>
                        {
                            await _server.BroadcastText(text);
                            Console.WriteLine("WebSocket server [{0}] broadcasts data -> [{1}].", _server.ListenedEndPoint, text);
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                _server.Stop().Wait();
                Console.WriteLine("WebSocket server has been stopped on [{0}].", _server.ListenedEndPoint);
            }
            catch (Exception ex)
            {
                Logger.Get<Program>().Error(ex.Message, ex);
            }

            Console.ReadKey();
        }

        private static async Task OnSessionStarted(AsyncWebSocketSession session)
        {
            Console.WriteLine(string.Format("WebSocket session [{0}] has connected [{1}].", session.RemoteEndPoint, session));
            await Task.CompletedTask;
        }

        private static async Task OnSessionTextReceived(AsyncWebSocketSession session, string text)
        {
            Console.Write(string.Format("WebSocket session [{0}] received Text --> ", session.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await session.SendText("Echo -> " + text);
        }

        private static async Task OnSessionDataReceived(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("WebSocket session [{0}] received Binary --> ", session.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await session.SendText("Echo -> " + text);
        }

        private static async Task OnSessionClosed(AsyncWebSocketSession session)
        {
            Console.WriteLine(string.Format("WebSocket session [{0}] has disconnected.", session.RemoteEndPoint));
            await Task.CompletedTask;
        }
    }
}
