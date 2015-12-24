using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public interface IAsyncTcpSocketServerMessageDispatcher
    {
        Task Dispatch(AsyncTcpSocketSession session, byte[] data);
        Task Dispatch(AsyncTcpSocketSession session, byte[] data, int dataOffset, int dataLength);
    }
}
