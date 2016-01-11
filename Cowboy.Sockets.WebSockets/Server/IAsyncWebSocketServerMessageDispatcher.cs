using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public interface IAsyncWebSocketServerMessageDispatcher
    {
        Task OnSessionStarted(AsyncWebSocketSession session);
        Task OnSessionTextReceived(AsyncWebSocketSession session, string text);
        Task OnSessionBinaryReceived(AsyncWebSocketSession session, byte[] data, int offset, int count);
        Task OnSessionClosed(AsyncWebSocketSession session);
    }
}
