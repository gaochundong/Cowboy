using System;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    internal class DefaultTcpSocketSaeaClientEventDispatcher : ITcpSocketSaeaClientEventDispatcher
    {
        private Func<TcpSocketSaeaClient, byte[], int, int, Task> _onServerDataReceived;
        private Func<TcpSocketSaeaClient, Task> _onServerConnected;
        private Func<TcpSocketSaeaClient, Task> _onServerDisconnected;

        public DefaultTcpSocketSaeaClientEventDispatcher()
        {
        }

        public DefaultTcpSocketSaeaClientEventDispatcher(
            Func<TcpSocketSaeaClient, byte[], int, int, Task> onServerDataReceived,
            Func<TcpSocketSaeaClient, Task> onServerConnected,
            Func<TcpSocketSaeaClient, Task> onServerDisconnected)
            : this()
        {
            _onServerDataReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;
        }

        public async Task OnServerConnected(TcpSocketSaeaClient client)
        {
            if (_onServerConnected != null)
                await _onServerConnected(client);
        }

        public async Task OnServerDataReceived(TcpSocketSaeaClient client, byte[] data, int offset, int count)
        {
            if (_onServerDataReceived != null)
                await _onServerDataReceived(client, data, offset, count);
        }

        public async Task OnServerDisconnected(TcpSocketSaeaClient client)
        {
            if (_onServerDisconnected != null)
                await _onServerDisconnected(client);
        }
    }
}
