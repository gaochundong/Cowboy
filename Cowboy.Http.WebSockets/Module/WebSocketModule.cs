using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cowboy.Http.WebSockets
{
    public abstract class WebSocketModule : IHideObjectMembers
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        private static readonly Regex ModuleNameExpression = new Regex(@"(?<name>[\w]+)Module$", RegexOptions.Compiled);

        private ConcurrentDictionary<string, WebSocketSession> _sessions = new ConcurrentDictionary<string, WebSocketSession>();

        protected WebSocketModule()
            : this(string.Empty)
        {
        }

        protected WebSocketModule(string modulePath)
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

        public async Task AcceptSession(WebSocketSession session)
        {
            if (_sessions.TryAdd(session.SessionKey, session))
            {
                try
                {
                    await session.Start();
                }
                finally
                {
                    WebSocketSession throwAway;
                    _sessions.TryRemove(session.SessionKey, out throwAway);
                }
            }
        }

        #region Receive

        public abstract Task ReceiveTextMessage(WebSocketTextMessage message);

        public abstract Task ReceiveBinaryMessage(WebSocketBinaryMessage message);

        #endregion

        #region Send

        public async Task Broadcast(string text)
        {
            foreach (var session in _sessions.Values)
            {
                await session.Send(text);
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
                await session.Send(binary, offset, count);
            }
        }

        #endregion
    }
}
