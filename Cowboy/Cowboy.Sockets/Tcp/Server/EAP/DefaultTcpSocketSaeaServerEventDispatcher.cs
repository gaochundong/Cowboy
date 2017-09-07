using System;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    internal class DefaultTcpSocketSaeaServerEventDispatcher : ITcpSocketSaeaServerEventDispatcher
    {
        private Func<TcpSocketSaeaSession, byte[], int, int, Task> _onSessionDataReceived;
        private Func<TcpSocketSaeaSession, Task> _onSessionStarted;
        private Func<TcpSocketSaeaSession, Task> _onSessionClosed;

        public DefaultTcpSocketSaeaServerEventDispatcher()
        {
        }

        public DefaultTcpSocketSaeaServerEventDispatcher(
            Func<TcpSocketSaeaSession, byte[], int, int, Task> onSessionDataReceived,
            Func<TcpSocketSaeaSession, Task> onSessionStarted,
            Func<TcpSocketSaeaSession, Task> onSessionClosed)
            : this()
        {
            _onSessionDataReceived = onSessionDataReceived;
            _onSessionStarted = onSessionStarted;
            _onSessionClosed = onSessionClosed;
        }

        public async Task OnSessionStarted(TcpSocketSaeaSession session)
        {
            if (_onSessionStarted != null)
                await _onSessionStarted(session);
        }

        public async Task OnSessionDataReceived(TcpSocketSaeaSession session, byte[] data, int offset, int count)
        {
            if (_onSessionDataReceived != null)
                await _onSessionDataReceived(session, data, offset, count);
        }

        public async Task OnSessionClosed(TcpSocketSaeaSession session)
        {
            if (_onSessionClosed != null)
                await _onSessionClosed(session);
        }
    }
}
