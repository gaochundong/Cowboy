using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestTcpSocketSaeaClient
{
    public class SimpleMessageDispatcher : ITcpSocketSaeaClientMessageDispatcher
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
            Console.WriteLine(string.Format("{0}", text));

            await Task.CompletedTask;
        }

        public async Task OnServerDisconnected(TcpSocketSaeaClient client)
        {
            Console.WriteLine(string.Format("TCP server {0} has disconnected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }
    }
}
