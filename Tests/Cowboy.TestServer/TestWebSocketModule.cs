using Cowboy.WebSockets;

namespace Cowboy.TestServer
{
    public class TestWebSocketModule : WebSocketModule
    {
        public TestWebSocketModule()
            :base(@"/test")
        {
        }
    }
}
