using System;
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

            _server = new AsyncTcpSocketServer(22222, new SimpleMessageDispatcher());
            _server.Start();

            Console.WriteLine("TCP server has been started on [{0}].", _server.ListenedEndPoint);
            Console.WriteLine("Type something to send to clients...");
            while (true)
            {
                try
                {
                    string text = Console.ReadLine();
                    Task.Run(async () =>
                    {
                        await _server.Broadcast(Encoding.UTF8.GetBytes(text));
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
