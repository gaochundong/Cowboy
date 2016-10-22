using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestAsyncTcpSocketServer
{
    public class SimpleMessageDispatcher : IAsyncTcpSocketServerMessageDispatcher
    {
        public async Task OnSessionStarted(AsyncTcpSocketSession session)
        {
            Console.WriteLine(string.Format("TCP session {0} has connected {1}.", session.RemoteEndPoint, session));
            await Task.CompletedTask;
        }

        public async Task OnSessionDataReceived(AsyncTcpSocketSession session, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("Client : {0} --> ", session.RemoteEndPoint));
            if (count < 1024 * 1024 * 1)
            {
                Console.WriteLine(text);
            }
            else
            {
                Console.WriteLine("{0} Bytes", count);
            }

            await session.SendAsync(Encoding.UTF8.GetBytes(text));
        }

        public async Task OnSessionClosed(AsyncTcpSocketSession session)
        {
            Console.WriteLine(string.Format("TCP session {0} has disconnected.", session));
            await Task.CompletedTask;
        }
    }
}
