using System;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.WebSockets.TestAsyncWebSocketServer
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
                //config.SslEnabled = true;
                //config.SslServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.pfx", "Cowboy");
                //config.SslPolicyErrorsBypassed = true;

                _server = new AsyncWebSocketServer(22222, catalog, config);
                _server.Listen();

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
                            if (text == "many")
                            {
                                text = new string('x', 8192);
                                for (int i = 0; i < 1000000; i++)
                                {
                                    await _server.BroadcastBinaryAsync(Encoding.UTF8.GetBytes(text));
                                    Console.WriteLine("WebSocket server [{0}] broadcasts binary -> [{1}].", _server.ListenedEndPoint, text);
                                }
                            }
                            else if (text == "big")
                            {
                                text = new string('x', 1024 * 1024 * 100);
                                await _server.BroadcastBinaryAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("WebSocket server [{0}] broadcasts binary -> [{1}].", _server.ListenedEndPoint, text);
                            }
                            else
                            {
                                await _server.BroadcastBinaryAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("WebSocket server [{0}] broadcasts binary -> [{1}].", _server.ListenedEndPoint, text);
                            }

                            //await _server.BroadcastText(text);
                            //Console.WriteLine("WebSocket server [{0}] broadcasts text -> [{1}].", _server.ListenedEndPoint, text);
                        });
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message);
                    }
                }

                _server.Shutdown();
                Console.WriteLine("WebSocket server has been stopped on [{0}].", _server.ListenedEndPoint);
            }
            catch (Exception ex)
            {
                Logger.Get<Program>().Error(ex.Message, ex);
            }

            Console.ReadKey();
        }
    }
}
