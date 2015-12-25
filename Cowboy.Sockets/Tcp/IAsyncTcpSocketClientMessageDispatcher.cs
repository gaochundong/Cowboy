using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public interface IAsyncTcpSocketClientMessageDispatcher
    {
        Task Dispatch(AsyncTcpSocketClient client, byte[] data, int offset, int count);
    }
}
