using System;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Sockets.Experimental;

namespace Cowboy.Sockets.TestTcpSocketRioServer
{
    public class SimpleMessageDispatcher : ITcpSocketRioServerMessageDispatcher
    {
        public async Task OnSessionStarted(TcpSocketRioSession session)
        {
            //Console.WriteLine(string.Format("TCP session {0} has connected {1}.", session.RemoteEndPoint, session));
            Console.WriteLine(string.Format("TCP session has connected {0}.", session));
            await Task.CompletedTask;
        }

        public async Task OnSessionDataReceived(TcpSocketRioSession session, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            //Console.Write(string.Format("Client : {0} --> ", session.RemoteEndPoint));
            Console.Write(string.Format("Client : --> "));
            Console.WriteLine(string.Format("{0}", text));

            await session.SendAsync(Encoding.UTF8.GetBytes(text));
        }

        public async Task OnSessionClosed(TcpSocketRioSession session)
        {
            Console.WriteLine(string.Format("TCP session {0} has disconnected.", session));
            await Task.CompletedTask;
        }
    }
}
