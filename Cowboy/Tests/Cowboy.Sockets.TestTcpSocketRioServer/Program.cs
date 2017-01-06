using System;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Sockets.Experimental;
using Logrila.Logging;
using Logrila.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestTcpSocketRioServer
{
    class Program
    {
        static TcpSocketRioServer _server;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            try
            {
                var config = new TcpSocketRioServerConfiguration();

                //config.FrameBuilder = new FixedLengthFrameBuilder(20000);
                //config.FrameBuilder = new RawBufferFrameBuilder();
                //config.FrameBuilder = new LineBasedFrameBuilder();
                //config.FrameBuilder = new LengthPrefixedFrameBuilder();
                //config.FrameBuilder = new LengthFieldBasedFrameBuilder();

                _server = new TcpSocketRioServer(22222, new SimpleMessageDispatcher(), config);
                _server.Listen();

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
                            if (text == "many")
                            {
                                text = new string('x', 8192);
                                for (int i = 0; i < 1000000; i++)
                                {
                                    await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                    Console.WriteLine("Server [{0}] broadcasts text -> [{1}].", _server.ListenedEndPoint, text);
                                }
                            }
                            else if (text == "big1k")
                            {
                                text = new string('x', 1024 * 1);
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                            else if (text == "big10k")
                            {
                                text = new string('x', 1024 * 10);
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                            else if (text == "big100k")
                            {
                                text = new string('x', 1024 * 100);
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                            else if (text == "big1m")
                            {
                                text = new string('x', 1024 * 1024 * 1);
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                            else if (text == "big10m")
                            {
                                text = new string('x', 1024 * 1024 * 10);
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                            else if (text == "big100m")
                            {
                                text = new string('x', 1024 * 1024 * 100);
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                            else if (text == "big1g")
                            {
                                text = new string('x', 1024 * 1024 * 1024);
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                            else
                            {
                                await _server.BroadcastAsync(Encoding.UTF8.GetBytes(text));
                                Console.WriteLine("Server [{0}] broadcasts text -> [{1} Bytes].", _server.ListenedEndPoint, text.Length);
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        Logger.Get<Program>().Error(ex.Message, ex);
                    }
                }

                _server.Shutdown();
                Console.WriteLine("TCP server has been stopped on [{0}].", _server.ListenedEndPoint);
            }
            catch (Exception ex)
            {
                Logger.Get<Program>().Error(ex.Message, ex);
            }

            Console.ReadKey();
        }
    }
}
