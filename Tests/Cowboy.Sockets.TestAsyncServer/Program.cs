using System;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestAsyncServer
{
    class Program
    {
        static AsyncTcpSocketServer _server;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            var config = new AsyncTcpSocketServerConfiguration();
            //config.UseSsl = true;
            //config.SslServerCertificate = new X509Certificate2(@"D:\\Cowboy.pfx", "Cowboy");
            //config.SslPolicyErrorsBypassed = false;

            _server = new AsyncTcpSocketServer(22222, new SimpleMessageDispatcher(), config);
            _server.Start();

            Console.WriteLine("TCP server has been started on [{0}].", _server.ListenedEndPoint);
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
                        await _server.Broadcast(Encoding.UTF8.GetBytes(text));
                        Console.WriteLine("Server [{0}] broadcasts data -> [{1}].", _server.ListenedEndPoint, text);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            _server.Stop();
            Console.WriteLine("TCP server has been stopped on [{0}].", _server.ListenedEndPoint);

            Console.ReadKey();
        }
    }
}
