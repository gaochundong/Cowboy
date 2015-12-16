using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
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
        }

        public string ModulePath { get; protected set; }

        public string GetModuleName()
        {
            var typeName = this.GetType().Name;
            var nameMatch = ModuleNameExpression.Match(typeName);

            if (nameMatch.Success)
            {
                return nameMatch.Groups["name"].Value;
            }

            return typeName;
        }

        public async Task AcceptSession(WebSocketSession session)
        {
            if (_sessions.TryAdd(session.RemoteEndPoint.ToString(), session))
            {
                await session.Start();

                WebSocketSession throwAway;
                _sessions.TryRemove(session.RemoteEndPoint.ToString(), out throwAway);
            }
        }

        public abstract Task ReceiveTextMessage(WebSocketTextMessage message);

        public abstract Task ReceiveBinaryMessage(WebSocketBinaryMessage message);

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

        public async Task SendTo(string endpoint, string text)
        {
            WebSocketSession session;
            if (_sessions.TryGetValue(endpoint, out session))
            {
                await session.Send(text);
            }
        }

        public async Task SendTo(string endpoint, byte[] binary)
        {
            await SendTo(endpoint, binary, 0, binary.Length);
        }

        public async Task SendTo(string endpoint, byte[] binary, int offset, int count)
        {
            WebSocketSession session;
            if (_sessions.TryGetValue(endpoint, out session))
            {
                await session.Send(binary, offset, count);
            }
        }
    }
}
