using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public interface ITcpSocketSaeaClientMessageDispatcher
    {
        Task OnServerConnected(TcpSocketSaeaClient client);
        Task OnServerDataReceived(TcpSocketSaeaClient client, byte[] data, int offset, int count);
        Task OnServerDisconnected(TcpSocketSaeaClient client);
    }
}
