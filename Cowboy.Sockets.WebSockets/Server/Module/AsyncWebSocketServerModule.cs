using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public abstract class AsyncWebSocketServerModule : IAsyncWebSocketServerMessageDispatcher, IHideObjectMembers
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Regex ModuleNameExpression = new Regex(@"(?<name>[\w]+)Module$", RegexOptions.Compiled);

        private ConcurrentDictionary<string, AsyncWebSocketSession> _sessions = new ConcurrentDictionary<string, AsyncWebSocketSession>();

        protected AsyncWebSocketServerModule()
            : this(string.Empty)
        {
        }

        protected AsyncWebSocketServerModule(string modulePath)
        {
            this.ModulePath = modulePath;
            this.ModuleName = GetModuleName();
        }

        private string GetModuleName()
        {
            var typeName = this.GetType().Name;
            var nameMatch = ModuleNameExpression.Match(typeName);

            if (nameMatch.Success)
            {
                return nameMatch.Groups["name"].Value;
            }

            return typeName;
        }

        public string ModuleName { get; protected set; }

        public string ModulePath { get; protected set; }

        public int SessionCount { get { return _sessions.Count; } }

        public void AcceptSession(AsyncWebSocketSession session)
        {
            _sessions.TryAdd(session.SessionKey, session);
        }

        public void RemoveSession(AsyncWebSocketSession session)
        {
            AsyncWebSocketSession throwAway;
            _sessions.TryRemove(session.SessionKey, out throwAway);
        }

        #region Dispatcher

        public abstract Task OnSessionStarted(AsyncWebSocketSession session);
        public abstract Task OnSessionTextReceived(AsyncWebSocketSession session, string text);
        public abstract Task OnSessionBinaryReceived(AsyncWebSocketSession session, byte[] data, int offset, int count);
        public abstract Task OnSessionClosed(AsyncWebSocketSession session);

        #endregion

        #region Send

        public async Task SendTo(string endpoint, string text)
        {
            AsyncWebSocketSession session;
            if (_sessions.TryGetValue(endpoint, out session))
            {
                await session.SendText(text);
            }
        }

        public async Task SendTo(string endpoint, byte[] binary)
        {
            await SendTo(endpoint, binary, 0, binary.Length);
        }

        public async Task SendTo(string endpoint, byte[] binary, int offset, int count)
        {
            AsyncWebSocketSession session;
            if (_sessions.TryGetValue(endpoint, out session))
            {
                await session.SendBinary(binary, offset, count);
            }
        }

        public async Task Broadcast(string text)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendText(text);
            }
        }

        public async Task Broadcast(byte[] binary)
        {
            await Broadcast(binary, 0, binary.Length);
        }

        public async Task Broadcast(byte[] binary, int offset, int count)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendBinary(binary, offset, count);
            }
        }

        #endregion
    }
}
