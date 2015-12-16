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

        private ConcurrentDictionary<IPEndPoint, WebSocketSession> _sessions = new ConcurrentDictionary<IPEndPoint, WebSocketSession>();

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
            if (_sessions.TryAdd(session.RemoteEndPoint, session))
            {
                await session.Start();

                WebSocketSession throwAway;
                _sessions.TryRemove(session.RemoteEndPoint, out throwAway);
            }
        }

        public async Task ReceiveTextMessage(WebSocketTextMessage message)
        {
            await message.Session.Send(message.Text);
        }

        public async Task ReceiveBinaryMessage(WebSocketBinaryMessage message)
        {
            await message.Session.Send(message.Buffer, message.Offset, message.Count);
        }
    }
}
