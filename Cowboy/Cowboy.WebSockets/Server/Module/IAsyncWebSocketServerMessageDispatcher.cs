using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public interface IAsyncWebSocketServerMessageDispatcher
    {
        Task OnSessionStarted(AsyncWebSocketSession session);
        Task OnSessionTextReceived(AsyncWebSocketSession session, string text);
        Task OnSessionBinaryReceived(AsyncWebSocketSession session, byte[] data, int offset, int count);
        Task OnSessionClosed(AsyncWebSocketSession session);

        Task OnSessionFragmentationStreamOpened(AsyncWebSocketSession session, byte[] data, int offset, int count);
        Task OnSessionFragmentationStreamContinued(AsyncWebSocketSession session, byte[] data, int offset, int count);
        Task OnSessionFragmentationStreamClosed(AsyncWebSocketSession session, byte[] data, int offset, int count);
    }
}
