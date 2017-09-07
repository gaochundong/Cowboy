using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestTcpSocketSaeaClient
{
    public class SimpleEventDispatcher : ITcpSocketSaeaClientEventDispatcher
    {
        public async Task OnServerConnected(TcpSocketSaeaClient client)
        {
            Console.WriteLine(string.Format("TCP server {0} has connected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }

        public async Task OnServerDataReceived(TcpSocketSaeaClient client, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("Server : {0} --> ", client.RemoteEndPoint));
            if (count < 1024 * 1024 * 1)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.WriteLine("{0} Bytes", count);
            }

            await Task.CompletedTask;
        }

        public async Task OnServerDisconnected(TcpSocketSaeaClient client)
        {
            Console.WriteLine(string.Format("TCP server {0} has disconnected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }
    }
}
