using System;
using System.Net;
using System.Text;
using Cowboy.Logging.NLogIntegration;

namespace Cowboy.Sockets.TestClient
{
    class Program
    {
        static TcpSocketClient client;

        static void Main(string[] args)
        {
            NLogLogger.Use();

            IPEndPoint remoteEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 22222);
            IPEndPoint localEP = new IPEndPoint(IPAddress.Parse("127.0.0.1"), 22221);
            client = new TcpSocketClient(remoteEP, localEP);
            client.IsPackingEnabled = true;
            client.ServerExceptionOccurred += client_ServerExceptionOccurred;
            client.ServerConnected += client_ServerConnected;
            client.ServerDisconnected += client_ServerDisconnected;
            client.DataReceived += client_DataReceived;
            client.Connect();

            Console.WriteLine("TCP client has connected to server.");
            Console.WriteLine("Type something to send to server...");
            while (true)
            {
                try
                {
                    string text = Console.ReadLine();
                    client.Send(Encoding.UTF8.GetBytes(text));
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        static void client_ServerExceptionOccurred(object sender, TcpServerExceptionOccurredEventArgs e)
        {
            Console.WriteLine(string.Format("TCP server {0} exception occurred, {1}.", e.ToString(), e.Exception.Message));
        }

        static void client_ServerConnected(object sender, TcpServerConnectedEventArgs e)
        {
            Console.WriteLine(string.Format("TCP server {0} has connected.", e.ToString()));
        }

        static void client_ServerDisconnected(object sender, TcpServerDisconnectedEventArgs e)
        {
            Console.WriteLine(string.Format("TCP server {0} has disconnected.", e.ToString()));
        }

        static void client_DataReceived(object sender, TcpDataReceivedEventArgs e)
        {
            var text = Encoding.UTF8.GetString(e.Data, e.DataOffset, e.DataLength);
            Console.Write(string.Format("Server : {0} --> ", e.Session.SessionKey));
            Console.WriteLine(string.Format("{0}", text));
        }
    }
}
