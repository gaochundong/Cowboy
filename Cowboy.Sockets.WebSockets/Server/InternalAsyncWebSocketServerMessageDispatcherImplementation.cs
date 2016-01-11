using System;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    internal class InternalAsyncWebSocketServerMessageDispatcherImplementation : IAsyncWebSocketServerMessageDispatcher
    {
        private Func<AsyncWebSocketSession, string, Task> _onSessionTextReceived;
        private Func<AsyncWebSocketSession, byte[], int, int, Task> _onSessionDataReceived;
        private Func<AsyncWebSocketSession, Task> _onSessionStarted;
        private Func<AsyncWebSocketSession, Task> _onSessionClosed;

        public InternalAsyncWebSocketServerMessageDispatcherImplementation()
        {
        }

        public InternalAsyncWebSocketServerMessageDispatcherImplementation(
            Func<AsyncWebSocketSession, string, Task> onSessionTextReceived,
            Func<AsyncWebSocketSession, byte[], int, int, Task> onSessionDataReceived,
            Func<AsyncWebSocketSession, Task> onSessionStarted,
            Func<AsyncWebSocketSession, Task> onSessionClosed)
            : this()
        {
            _onSessionTextReceived = onSessionTextReceived;
            _onSessionDataReceived = onSessionDataReceived;
            _onSessionStarted = onSessionStarted;
            _onSessionClosed = onSessionClosed;
        }

        public async Task OnSessionStarted(AsyncWebSocketSession session)
        {
            if (_onSessionStarted != null)
                await _onSessionStarted(session);
        }

        public async Task OnSessionTextReceived(AsyncWebSocketSession session, string text)
        {
            if (_onSessionTextReceived != null)
                await _onSessionTextReceived(session, text);
        }

        public async Task OnSessionBinaryReceived(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            if (_onSessionDataReceived != null)
                await _onSessionDataReceived(session, data, offset, count);
        }

        public async Task OnSessionClosed(AsyncWebSocketSession session)
        {
            if (_onSessionClosed != null)
                await _onSessionClosed(session);
        }
    }
}
