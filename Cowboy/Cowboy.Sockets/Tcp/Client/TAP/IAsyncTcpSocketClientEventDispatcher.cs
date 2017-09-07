using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public interface IAsyncTcpSocketClientEventDispatcher
    {
        Task OnServerConnected(AsyncTcpSocketClient client);
        Task OnServerDataReceived(AsyncTcpSocketClient client, byte[] data, int offset, int count);
        Task OnServerDisconnected(AsyncTcpSocketClient client);
    }
}
