using System.Threading.Tasks;
using Cowboy.WebSockets;

namespace Cowboy.TestServer
{
    public class TestWebSocketModule : WebSocketModule
    {
        public TestWebSocketModule()
            : base(@"/")
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
