using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public interface IAsyncWebSocketClientMessageDispatcher
    {
        Task OnServerConnected(AsyncWebSocketClient client);
        Task OnServerTextReceived(AsyncWebSocketClient client, string text);
        Task OnServerBinaryReceived(AsyncWebSocketClient client, byte[] data, int offset, int count);
        Task OnServerDisconnected(AsyncWebSocketClient client);

        Task OnServerFragmentationStreamOpened(AsyncWebSocketClient client, byte[] data, int offset, int count);
        Task OnServerFragmentationStreamContinued(AsyncWebSocketClient client, byte[] data, int offset, int count);
        Task OnServerFragmentationStreamClosed(AsyncWebSocketClient client, byte[] data, int offset, int count);
    }
}
