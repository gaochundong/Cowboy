using System;
using System.Threading.Tasks;

namespace Cowboy.Sockets.Experimental
{
    internal class InternalTcpSocketRioServerMessageDispatcherImplementation : ITcpSocketRioServerMessageDispatcher
    {
        private Func<TcpSocketRioSession, byte[], int, int, Task> _onSessionDataReceived;
        private Func<TcpSocketRioSession, Task> _onSessionStarted;
        private Func<TcpSocketRioSession, Task> _onSessionClosed;

        public InternalTcpSocketRioServerMessageDispatcherImplementation()
        {
        }

        public InternalTcpSocketRioServerMessageDispatcherImplementation(
            Func<TcpSocketRioSession, byte[], int, int, Task> onSessionDataReceived,
            Func<TcpSocketRioSession, Task> onSessionStarted,
            Func<TcpSocketRioSession, Task> onSessionClosed)
            : this()
        {
            _onSessionDataReceived = onSessionDataReceived;
            _onSessionStarted = onSessionStarted;
            _onSessionClosed = onSessionClosed;
        }

        public async Task OnSessionStarted(TcpSocketRioSession session)
        {
            if (_onSessionStarted != null)
                await _onSessionStarted(session);
        }

        public async Task OnSessionDataReceived(TcpSocketRioSession session, byte[] data, int offset, int count)
        {
            if (_onSessionDataReceived != null)
                await _onSessionDataReceived(session, data, offset, count);
        }

        public async Task OnSessionClosed(TcpSocketRioSession session)
        {
            if (_onSessionClosed != null)
                await _onSessionClosed(session);
        }
    }
}
