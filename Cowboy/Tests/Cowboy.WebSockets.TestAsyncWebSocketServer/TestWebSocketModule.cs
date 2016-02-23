using System;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.TestAsyncWebSocketServer
{
    public class TestWebSocketModule : AsyncWebSocketServerModule
    {
        public TestWebSocketModule()
            : base(@"/test")
        {
        }

        public override async Task OnSessionStarted(AsyncWebSocketSession session)
        {
            Console.WriteLine(string.Format("WebSocket session [{0}] has connected.", session.RemoteEndPoint));
            await Task.CompletedTask;
        }

        public override async Task OnSessionTextReceived(AsyncWebSocketSession session, string text)
        {
            Console.Write(string.Format("WebSocket session [{0}] received Text --> ", session.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            await session.SendTextAsync(text);
        }

        public override async Task OnSessionBinaryReceived(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            var text = Encoding.UTF8.GetString(data, offset, count);
            Console.Write(string.Format("WebSocket session [{0}] received Binary --> ", session.RemoteEndPoint));
            Console.WriteLine(string.Format("{0}", text));

            //await Task.Delay(TimeSpan.FromSeconds(10));
            //await Task.CompletedTask;

            await session.SendBinaryAsync(Encoding.UTF8.GetBytes(text));
        }

        public override async Task OnSessionClosed(AsyncWebSocketSession session)
        {
            Console.WriteLine(string.Format("WebSocket session [{0}] has disconnected.", session.RemoteEndPoint));
            await Task.CompletedTask;
        }
    }
}
