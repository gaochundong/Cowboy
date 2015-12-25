using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestAsyncClient
{
    public class SimpleMessageDispatcher : IAsyncTcpSocketClientMessageDispatcher
    {
        public async Task Dispatch(AsyncTcpSocketClient client, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("Server : {0} --> ", client.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await Task.Yield();
        }
    }
}
