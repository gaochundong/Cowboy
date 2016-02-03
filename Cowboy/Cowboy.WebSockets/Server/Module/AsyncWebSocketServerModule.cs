using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public abstract class AsyncWebSocketServerModule : IAsyncWebSocketServerMessageDispatcher
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

        #region Dispatcher

        public virtual async Task OnSessionStarted(AsyncWebSocketSession session)
        {
            _sessions.TryAdd(session.SessionKey, session);
            await Task.CompletedTask;
        }

        public virtual async Task OnSessionTextReceived(AsyncWebSocketSession session, string text)
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnSessionBinaryReceived(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnSessionClosed(AsyncWebSocketSession session)
        {
            AsyncWebSocketSession throwAway;
            _sessions.TryRemove(session.SessionKey, out throwAway);
            await Task.CompletedTask;
        }

        #endregion

        #region Fragmentation

        public virtual async Task OnSessionFragmentationStreamOpened(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnSessionFragmentationStreamContinued(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            await Task.CompletedTask;
        }

        public virtual async Task OnSessionFragmentationStreamClosed(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            await Task.CompletedTask;
        }

        #endregion

        #region Send

        public async Task Broadcast(string text)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendTextAsync(text);
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
                await session.SendBinaryAsync(binary, offset, count);
            }
        }

        #endregion
    }
}
