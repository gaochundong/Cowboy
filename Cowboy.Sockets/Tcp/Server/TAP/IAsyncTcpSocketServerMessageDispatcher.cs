using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public interface IAsyncTcpSocketServerMessageDispatcher
    {
        Task OnSessionStarted(AsyncTcpSocketSession session);
        Task OnSessionDataReceived(AsyncTcpSocketSession session, byte[] data, int offset, int count);
        Task OnSessionClosed(AsyncTcpSocketSession session);
    }
}
