using System.Threading.Tasks;

namespace Cowboy.Sockets.Experimental
{
    public interface ITcpSocketRioServerMessageDispatcher
    {
        Task OnSessionStarted(TcpSocketRioSession session);
        Task OnSessionDataReceived(TcpSocketRioSession session, byte[] data, int offset, int count);
        Task OnSessionClosed(TcpSocketRioSession session);
    }
}
