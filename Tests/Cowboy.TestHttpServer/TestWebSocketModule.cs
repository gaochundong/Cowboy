using System.Threading.Tasks;
using Cowboy.Http.WebSockets;

namespace Cowboy.TestHttpServer
{
    public class TestWebSocketModule : WebSocketModule
    {
        public TestWebSocketModule()
            : base(@"/test")
        {
        }

        public override async Task ReceiveTextMessage(WebSocketTextMessage message)
        {
            await message.Session.Send(message.Text);
        }

        public override async Task ReceiveBinaryMessage(WebSocketBinaryMessage message)
        {
            await message.Session.Send(message.Buffer, message.Offset, message.Count);
        }
    }
}
