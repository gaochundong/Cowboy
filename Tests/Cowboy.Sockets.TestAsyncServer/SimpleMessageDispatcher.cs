using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestAsyncServer
{
    public class SimpleMessageDispatcher : IAsyncTcpSocketServerMessageDispatcher
    {
        public async Task Dispatch(AsyncTcpSocketSession session, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("Client : {0} --> ", session.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await session.Send(Encoding.UTF8.GetBytes("Echo -> " + text));
        }
    }
}
