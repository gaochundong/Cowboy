using System;
using System.Text;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestTcpSocketServer
{
    class Program
    {
        static TcpSocketServer _server;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            StartServer();

            Console.WriteLine("TCP server has been started.");
            Console.WriteLine("Type something to send to client...");
            while (true)
            {
                try
                {
                    string text = Console.ReadLine();
                    if (text == "quit")
                    {
                        break;
                    }
                    else if (text == "many")
                    {
                        text = new string('x', 8192);
                        for (int i = 0; i < 1000000; i++)
                        {
                            _server.Broadcast(Encoding.UTF8.GetBytes(text));
                        }
                    }
                    else if (text == "big")
                    {
                        text = new string('x', 1024 * 1024 * 100);
                        _server.Broadcast(Encoding.UTF8.GetBytes(text));
                    }
                    else
                    {
                        _server.Broadcast(Encoding.UTF8.GetBytes(text));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }

            _server.Shutdown();
            Console.WriteLine("TCP server has been stopped on [{0}].", _server.ListenedEndPoint);

            Console.ReadKey();
        }

        private static void StartServer()
        {
            var config = new TcpSocketServerConfiguration();
            //config.UseSsl = true;
            //config.SslServerCertificate = new System.Security.Cryptography.X509Certificates.X509Certificate2(@"D:\\Cowboy.pfx", "Cowboy");
            //config.SslPolicyErrorsBypassed = false;
            config.FrameBuilder = new FixedLengthFrameBuilder(5);

            _server = new TcpSocketServer(22222, config);
            _server.ClientConnected += server_ClientConnected;
            _server.ClientDisconnected += server_ClientDisconnected;
            _server.ClientDataReceived += server_ClientDataReceived;
            _server.Listen();
        }

        static void server_ClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            //Console.WriteLine(string.Format("TCP client {0} has connected {1}.", e.Session.RemoteEndPoint, e.Session));
        }

        static void server_ClientDisconnected(object sender, TcpClientDisconnectedEventArgs e)
        {
            //Console.WriteLine(string.Format("TCP client {0} has disconnected.", e.Session));
        }

        static void server_ClientDataReceived(object sender, TcpClientDataReceivedEventArgs e)
        {
            var text = Encoding.UTF8.GetString(e.Data, e.DataOffset, e.DataLength);
            //Console.Write(string.Format("Client : {0} {1} --> ", e.Session.RemoteEndPoint, e.Session));
            //Console.WriteLine(string.Format("{0}", text));
            //_server.Broadcast(Encoding.UTF8.GetBytes(text));
            _server.SendTo(e.Session, Encoding.UTF8.GetBytes(text));
        }
    }
}
