using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public interface IAsyncWebSocketClientMessageDispatcher
    {
        Task OnServerConnected(AsyncWebSocketClient client);
        Task OnServerDataReceived(AsyncWebSocketClient client, byte[] data, int offset, int count);
        Task OnServerDisconnected(AsyncWebSocketClient client);
    }
}
