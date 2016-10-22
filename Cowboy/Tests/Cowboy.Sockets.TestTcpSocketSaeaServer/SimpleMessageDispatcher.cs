using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.TestTcpSocketSaeaServer
{
    public class SimpleMessageDispatcher : ITcpSocketSaeaServerMessageDispatcher
    {
        public async Task OnSessionStarted(TcpSocketSaeaSession session)
        {
            Console.WriteLine(string.Format("TCP session {0} has connected {1}.", session.RemoteEndPoint, session));
            await Task.CompletedTask;
        }

        public async Task OnSessionDataReceived(TcpSocketSaeaSession session, byte[] data, int offset, int count)
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

        public async Task OnSessionClosed(TcpSocketSaeaSession session)
        {
            Console.WriteLine(string.Format("TCP session {0} has disconnected.", session));
            await Task.CompletedTask;
        }
    }
}
