using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestAsyncClient
{
    class Program
    {
        static AsyncTcpSocketClient _client;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            var config = new AsyncTcpSocketClientConfiguration();
            //config.UseSsl = true;
            //config.SslTargetHost = "Cowboy";
            //config.SslClientCertificates.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.cer"));
            //config.SslPolicyErrorsBypassed = false;

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 22222);
            _client = new AsyncTcpSocketClient(remoteEP, new SimpleMessageDispatcher(), config);
            _client.Connect();

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
                        await _client.Send(Encoding.UTF8.GetBytes(text));
                        Console.WriteLine("Client [{0}] send data -> [{1}].", _client.LocalEndPoint, text);
                    });
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            _client.Close().Wait();
            Console.WriteLine("TCP client has disconnected from server [{0}].", remoteEP);

            Console.ReadKey();
        }
    }
}
