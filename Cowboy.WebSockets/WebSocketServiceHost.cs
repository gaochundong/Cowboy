using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public abstract class WebSocketServiceHost
    {
        protected WebSocketServiceHost()
        {
        }

        internal ServerState State
        {
            get
            {
                return Sessions.State;
            }
        }

        public abstract bool KeepClean { get; set; }

        public abstract string Path { get; }

        public abstract WebSocketSessionManager Sessions { get; }

        public abstract Type Type { get; }

        public abstract TimeSpan WaitTime { get; set; }

        internal void Start()
        {
            Sessions.Start();
        }

        internal void StartSession(WebSocketContext context)
        {
            CreateSession().Start(context, Sessions);
        }

        internal void Stop(ushort code, string reason)
        {
            var e = new CloseEventArgs(code, reason);
            var send = !code.IsReserved();
            var bytes = send ? WebSocketFrame.CreateCloseFrame(e.PayloadData, false).ToArray() : null;
            Sessions.Stop(e, bytes, send);
        }

        protected abstract WebSocketBehavior CreateSession();
    }

    internal class WebSocketServiceHost<TBehavior> : WebSocketServiceHost
        where TBehavior : WebSocketBehavior
    {
        private Func<TBehavior> _initializer;
        private string _path;
        private WebSocketSessionManager _sessions;

        internal WebSocketServiceHost(string path, Func<TBehavior> initializer)
        {
            _path = path;
            _initializer = initializer;
            _sessions = new WebSocketSessionManager();
        }

        public override bool KeepClean
        {
            get
            {
                return _sessions.KeepClean;
            }
            set
            {
                var msg = _sessions.State.CheckIfAvailable(true, false, false);
                if (msg != null)
                {
                    return;
                }

                _sessions.KeepClean = value;
            }
        }

        public override string Path
        {
            get
            {
                return _path;
            }
        }

        public override WebSocketSessionManager Sessions
        {
            get
            {
                return _sessions;
            }
        }

        public override Type Type
        {
            get
            {
                return typeof(TBehavior);
            }
        }

        public override TimeSpan WaitTime
        {
            get
            {
                return _sessions.WaitTime;
            }

            set
            {
                var msg = _sessions.State.CheckIfAvailable(true, false, false) ??
                          value.CheckIfValidWaitTime();

                if (msg != null)
                {
                    _logger.Error(msg);
                    return;
                }

                _sessions.WaitTime = value;
            }
        }

        protected override WebSocketBehavior CreateSession()
        {
            return _initializer();
        }
    }
}
