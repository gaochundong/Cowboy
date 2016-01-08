using System;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    internal class InternalAsyncWebSocketClientMessageDispatcherImplementation : IAsyncWebSocketClientMessageDispatcher
    {
        private Func<AsyncWebSocketClient, byte[], int, int, Task> _onServerDataReceived;
        private Func<AsyncWebSocketClient, Task> _onServerConnected;
        private Func<AsyncWebSocketClient, Task> _onServerDisconnected;

        public InternalAsyncWebSocketClientMessageDispatcherImplementation()
        {
        }

        public InternalAsyncWebSocketClientMessageDispatcherImplementation(
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerDataReceived,
            Func<AsyncWebSocketClient, Task> onServerConnected,
            Func<AsyncWebSocketClient, Task> onServerDisconnected)
            : this()
        {
            _onServerDataReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;
        }

        public async Task OnServerConnected(AsyncWebSocketClient client)
        {
            if (_onServerConnected != null)
                await _onServerConnected(client);
        }

        public async Task OnServerDataReceived(AsyncWebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerDataReceived != null)
                await _onServerDataReceived(client, data, offset, count);
        }

        public async Task OnServerDisconnected(AsyncWebSocketClient client)
        {
            if (_onServerDisconnected != null)
                await _onServerDisconnected(client);
        }
    }
}
