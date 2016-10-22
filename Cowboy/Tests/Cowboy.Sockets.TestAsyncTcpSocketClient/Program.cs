using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Logrila.Logging;
using Logrila.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestAsyncTcpSocketClient
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
                //config.UseSsl = true;
                //config.SslTargetHost = "Cowboy";
                //config.SslClientCertificates.Add(new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.cer"));
                //config.SslPolicyErrorsBypassed = false;

                //config.FrameBuilder = new FixedLengthFrameBuilder(20000);
                //config.FrameBuilder = new RawBufferFrameBuilder();
                //config.FrameBuilder = new LineBasedFrameBuilder();
                //config.FrameBuilder = new LengthPrefixedFrameBuilder();
                //config.FrameBuilder = new LengthFieldBasedFrameBuilder();

                IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 22222);
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
                            if (text == "many")
                            {
                                text = new string('x', 8192);
                                for (int i = 0; i < 1000000; i++)
                                {
                                    await _client.SendAsync(Encoding.UTF8.GetBytes(text));
                                    Console.WriteLine("Client [{0}] send text -> [{1}].", _client.LocalEndPoint, text);
                                }
                            }
                            else if (text == "big1")
                            {
                                text = new string('x', 1024 * 1024 * 1);
                                await _client.SendAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Client [{0}] send text -> [{1} Bytes].", _client.LocalEndPoint, text.Length);
                            }
                            else if (text == "big10")
                            {
                                text = new string('x', 1024 * 1024 * 10);
                                await _client.SendAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Client [{0}] send text -> [{1} Bytes].", _client.LocalEndPoint, text.Length);
                            }
                            else if (text == "big100")
                            {
                                text = new string('x', 1024 * 1024 * 100);
                                await _client.SendAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Client [{0}] send text -> [{1} Bytes].", _client.LocalEndPoint, text.Length);
                            }
                            else if (text == "big1000")
                            {
                                text = new string('x', 1024 * 1024 * 1000);
                                await _client.SendAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Client [{0}] send text -> [{1} Bytes].", _client.LocalEndPoint, text.Length);
                            }
                            else
                            {
                                await _client.SendAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Client [{0}] send text -> [{1} Bytes].", _client.LocalEndPoint, text.Length);
                            }
                        });
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
    }
}
