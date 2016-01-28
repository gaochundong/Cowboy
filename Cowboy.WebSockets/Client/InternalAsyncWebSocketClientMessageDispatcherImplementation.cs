using System;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    internal class InternalAsyncWebSocketClientMessageDispatcherImplementation : IAsyncWebSocketClientMessageDispatcher
    {
        private Func<AsyncWebSocketClient, string, Task> _onServerTextReceived;
        private Func<AsyncWebSocketClient, byte[], int, int, Task> _onServerBinaryReceived;
        private Func<AsyncWebSocketClient, Task> _onServerConnected;
        private Func<AsyncWebSocketClient, Task> _onServerDisconnected;

        private Func<AsyncWebSocketClient, byte[], int, int, Task> _onServerFragmentationStreamOpened;
        private Func<AsyncWebSocketClient, byte[], int, int, Task> _onServerFragmentationStreamContinued;
        private Func<AsyncWebSocketClient, byte[], int, int, Task> _onServerFragmentationStreamClosed;

        public InternalAsyncWebSocketClientMessageDispatcherImplementation()
        {
        }

        public InternalAsyncWebSocketClientMessageDispatcherImplementation(
            Func<AsyncWebSocketClient, string, Task> onServerTextReceived,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerDataReceived,
            Func<AsyncWebSocketClient, Task> onServerConnected,
            Func<AsyncWebSocketClient, Task> onServerDisconnected)
            : this()
        {
            _onServerTextReceived = onServerTextReceived;
            _onServerBinaryReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;
        }

        public InternalAsyncWebSocketClientMessageDispatcherImplementation(
            Func<AsyncWebSocketClient, string, Task> onServerTextReceived,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerDataReceived,
            Func<AsyncWebSocketClient, Task> onServerConnected,
            Func<AsyncWebSocketClient, Task> onServerDisconnected,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerFragmentationStreamOpened,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerFragmentationStreamContinued,
            Func<AsyncWebSocketClient, byte[], int, int, Task> onServerFragmentationStreamClosed)
            : this()
        {
            _onServerTextReceived = onServerTextReceived;
            _onServerBinaryReceived = onServerDataReceived;
            _onServerConnected = onServerConnected;
            _onServerDisconnected = onServerDisconnected;

            _onServerFragmentationStreamOpened = onServerFragmentationStreamOpened;
            _onServerFragmentationStreamContinued = onServerFragmentationStreamContinued;
            _onServerFragmentationStreamClosed = onServerFragmentationStreamClosed;
        }

        public async Task OnServerConnected(AsyncWebSocketClient client)
        {
            if (_onServerConnected != null)
                await _onServerConnected(client);
        }

        public async Task OnServerTextReceived(AsyncWebSocketClient client, string text)
        {
            if (_onServerTextReceived != null)
                await _onServerTextReceived(client, text);
        }

        public async Task OnServerBinaryReceived(AsyncWebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerBinaryReceived != null)
                await _onServerBinaryReceived(client, data, offset, count);
        }

        public async Task OnServerDisconnected(AsyncWebSocketClient client)
        {
            if (_onServerDisconnected != null)
                await _onServerDisconnected(client);
        }

        public async Task OnServerFragmentationStreamOpened(AsyncWebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerFragmentationStreamOpened != null)
                await _onServerFragmentationStreamOpened(client, data, offset, count);
        }

        public async Task OnServerFragmentationStreamContinued(AsyncWebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerFragmentationStreamContinued != null)
                await _onServerFragmentationStreamContinued(client, data, offset, count);
        }

        public async Task OnServerFragmentationStreamClosed(AsyncWebSocketClient client, byte[] data, int offset, int count)
        {
            if (_onServerFragmentationStreamClosed != null)
                await _onServerFragmentationStreamClosed(client, data, offset, count);
        }
    }
}
