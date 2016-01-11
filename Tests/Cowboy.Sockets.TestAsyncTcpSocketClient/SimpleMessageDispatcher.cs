using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestAsyncTcpSocketClient
{
    public class SimpleMessageDispatcher : IAsyncTcpSocketClientMessageDispatcher
    {
        public async Task OnServerConnected(AsyncTcpSocketClient client)
        {
            Console.WriteLine(string.Format("TCP server {0} has connected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }

        public async Task OnServerDataReceived(AsyncTcpSocketClient client, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("Server : {0} --> ", client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await Task.CompletedTask;
        }

        public async Task OnServerDisconnected(AsyncTcpSocketClient client)
        {
            Console.WriteLine(string.Format("TCP server {0} has disconnected.", client.RemoteEndPoint));
            await Task.CompletedTask;
        }
    }
}
