using System;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    internal class InternalAsyncTcpSocketServerMessageDispatcherImplementation : IAsyncTcpSocketServerMessageDispatcher
    {
        private Func<AsyncTcpSocketSession, byte[], int, int, Task> _onSessionDataReceived;
        private Func<AsyncTcpSocketSession, Task> _onSessionStarted;
        private Func<AsyncTcpSocketSession, Task> _onSessionClosed;

        public InternalAsyncTcpSocketServerMessageDispatcherImplementation()
        {
        }

        public InternalAsyncTcpSocketServerMessageDispatcherImplementation(
            Func<AsyncTcpSocketSession, byte[], int, int, Task> onSessionDataReceived,
            Func<AsyncTcpSocketSession, Task> onSessionStarted,
            Func<AsyncTcpSocketSession, Task> onSessionClosed)
            : this()
        {
            _onSessionDataReceived = onSessionDataReceived;
            _onSessionStarted = onSessionStarted;
            _onSessionClosed = onSessionClosed;
        }

        public async Task OnSessionStarted(AsyncTcpSocketSession session)
        {
            if (_onSessionStarted != null)
                await _onSessionStarted(session);
        }

        public async Task OnSessionDataReceived(AsyncTcpSocketSession session, byte[] data, int offset, int count)
        {
            if (_onSessionDataReceived != null)
                await _onSessionDataReceived(session, data, offset, count);
        }

        public async Task OnSessionClosed(AsyncTcpSocketSession session)
        {
            if (_onSessionClosed != null)
                await _onSessionClosed(session);
        }
    }
}
