using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestAsyncServer
{
    public class SimpleMessageDispatcher : IAsyncTcpSocketServerMessageDispatcher
    {
        public async Task Dispatch(AsyncTcpSocketSession session, byte[] data)
        {
            await Dispatch(session, data, 0, data.Length);
        }

        public async Task Dispatch(AsyncTcpSocketSession session, byte[] data, int dataOffset, int dataLength)
        {
            var text = Encoding.UTF8.GetString(data, dataOffset, dataLength);
            Console.Write(string.Format("Client : {0} --> ", session.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));
            await session.Send(Encoding.UTF8.GetBytes("Echo -> " + text));
        }
    }
}
