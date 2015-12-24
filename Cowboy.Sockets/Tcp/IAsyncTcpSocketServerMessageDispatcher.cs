using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public interface IAsyncTcpSocketServerMessageDispatcher
    {
        Task Dispatch(AsyncTcpSocketSession session, byte[] data, int offset, int count);
    }
}
