using System;
using System.Net;
using System.Security.Cryptography.X509Certificates;
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
            config.UseSsl = true;
            config.SslClientCertificates.Add(new X509Certificate2(@"D:\\CowboyClient.cer"));
            config.SslPolicyErrorsBypassed = true;

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 22222);
            IPEndPoint localEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 22221);
            _client = new AsyncTcpSocketClient(remoteEP, localEP, new SimpleMessageDispatcher(), config);
            _client.Connect();

            Console.WriteLine("TCP client has connected to server [{0}].", remoteEP);
            Console.WriteLine("Type something to send to server...");
            while (true)
            {
                try
                {
                    string text = Console.ReadLine();
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
        }
    }
}
