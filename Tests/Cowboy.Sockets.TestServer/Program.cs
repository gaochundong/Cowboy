using System;
using System.Text;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestServer
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
                string text = Console.ReadLine();
                _server.SendToAll(Encoding.UTF8.GetBytes(text));
            }
        }

        private static void StartServer()
        {
            _server = new TcpSocketServer(22222);
            _server.ClientConnected += server_ClientConnected;
            _server.ClientDisconnected += server_ClientDisconnected;
            _server.DataReceived += server_DataReceived;
            _server.Start();
        }

        static void server_ClientConnected(object sender, TcpClientConnectedEventArgs e)
        {
            Console.WriteLine(string.Format("TCP client {0} has connected.", e.Session.SessionKey));
        }

        static void server_ClientDisconnected(object sender, TcpClientDisconnectedEventArgs e)
        {
            Console.WriteLine(string.Format("TCP client {0} has disconnected.", e.Session.SessionKey));
        }

        static void server_DataReceived(object sender, TcpDataReceivedEventArgs e)
        {
            var text = Encoding.UTF8.GetString(e.Data, e.DataOffset, e.DataLength);
            Console.Write(string.Format("Client : {0} --> ", e.Session.SessionKey));
            Console.WriteLine(string.Format("{0}", text));
            _server.SendToAll(Encoding.UTF8.GetBytes(text));
        }
    }
}
