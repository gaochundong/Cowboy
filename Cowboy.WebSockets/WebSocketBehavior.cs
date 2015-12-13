using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public abstract class WebSocketBehavior : IWebSocketSession
    {
        private WebSocketContext _context;
        private Func<CookieCollection, CookieCollection, bool> _cookiesValidator;
        private bool _emitOnPing;
        private string _id;
        private bool _ignoreExtensions;
        private Func<string, bool> _originValidator;
        private string _protocol;
        private WebSocketSessionManager _sessions;
        private DateTime _startTime;
        private WebSocket _websocket;

        protected WebSocketBehavior()
        {
            _startTime = DateTime.MaxValue;
        }

        protected Logger Log
        {
            get
            {
                return _websocket != null ? _websocket.Log : null;
            }
        }

        protected WebSocketSessionManager Sessions
        {
            get
            {
                return _sessions;
            }
        }

        public WebSocketContext Context
        {
            get
            {
                return _context;
            }
        }

        public Func<CookieCollection, CookieCollection, bool> CookiesValidator
        {
            get
            {
                return _cookiesValidator;
            }

            set
            {
                _cookiesValidator = value;
            }
        }

        public bool EmitOnPing
        {
            get
            {
                return _websocket != null ? _websocket.EmitOnPing : _emitOnPing;
            }

            set
            {
                if (_websocket != null)
                {
                    _websocket.EmitOnPing = value;
                    return;
                }

                _emitOnPing = value;
            }
        }

        public string ID
        {
            get
            {
                return _id;
            }
        }

        public bool IgnoreExtensions
        {
            get
            {
                return _ignoreExtensions;
            }

            set
            {
                _ignoreExtensions = value;
            }
        }

        public Func<string, bool> OriginValidator
        {
            get
            {
                return _originValidator;
            }

            set
            {
                _originValidator = value;
            }
        }

        public string Protocol
        {
            get
            {
                return _websocket != null ? _websocket.Protocol : (_protocol ?? String.Empty);
            }

            set
            {
                if (State != WebSocketState.Connecting)
                    return;

                if (value != null && (value.Length == 0 || !value.IsToken()))
                    return;

                _protocol = value;
            }
        }

        public DateTime StartTime
        {
            get
            {
                return _startTime;
            }
        }

        public WebSocketState State
        {
            get
            {
                return _websocket != null ? _websocket.ReadyState : WebSocketState.Connecting;
            }
        }

        private string checkIfValidConnectionRequest(WebSocketContext context)
        {
            return _originValidator != null && !_originValidator(context.Origin)
                   ? "Invalid Origin header."
                   : _cookiesValidator != null &&
                     !_cookiesValidator(context.CookieCollection, context.WebSocket.CookieCollection)
                     ? "Invalid Cookies."
                     : null;
        }

        private void onClose(object sender, CloseEventArgs e)
        {
            if (_id == null)
                return;

            _sessions.Remove(_id);
            OnClose(e);
        }

        private void onError(object sender, ErrorEventArgs e)
        {
            OnError(e);
        }

        private void onMessage(object sender, MessageEventArgs e)
        {
            OnMessage(e);
        }

        private void onOpen(object sender, EventArgs e)
        {
            _id = _sessions.Add(this);
            if (_id == null)
            {
                _websocket.Close(CloseStatusCode.Away);
                return;
            }

            _startTime = DateTime.Now;
            OnOpen();
        }

        internal void Start(WebSocketContext context, WebSocketSessionManager sessions)
        {
            if (_websocket != null)
            {
                _websocket.Log.Error("This session has already been started.");
                context.WebSocket.Close(HttpStatusCode.ServiceUnavailable);

                return;
            }

            _context = context;
            _sessions = sessions;

            _websocket = context.WebSocket;
            _websocket.CustomHandshakeRequestChecker = checkIfValidConnectionRequest;
            _websocket.EmitOnPing = _emitOnPing;
            _websocket.IgnoreExtensions = _ignoreExtensions;
            _websocket.Protocol = _protocol;

            var waitTime = sessions.WaitTime;
            if (waitTime != _websocket.WaitTime)
                _websocket.WaitTime = waitTime;

            _websocket.OnOpen += onOpen;
            _websocket.OnMessage += onMessage;
            _websocket.OnError += onError;
            _websocket.OnClose += onClose;

            _websocket.InternalAccept();
        }

        protected void Error(string message, Exception exception)
        {
            if (message != null && message.Length > 0)
                OnError(new ErrorEventArgs(message, exception));
        }

        protected virtual void OnClose(CloseEventArgs e)
        {
        }

        protected virtual void OnError(ErrorEventArgs e)
        {
        }

        protected virtual void OnMessage(MessageEventArgs e)
        {
        }

        protected virtual void OnOpen()
        {
        }

        protected void Send(byte[] data)
        {
            if (_websocket != null)
                _websocket.Send(data);
        }

        protected void Send(FileInfo file)
        {
            if (_websocket != null)
                _websocket.Send(file);
        }

        protected void Send(string data)
        {
            if (_websocket != null)
                _websocket.Send(data);
        }

        protected void SendAsync(byte[] data, Action<bool> completed)
        {
            if (_websocket != null)
                _websocket.SendAsync(data, completed);
        }

        protected void SendAsync(FileInfo file, Action<bool> completed)
        {
            if (_websocket != null)
                _websocket.SendAsync(file, completed);
        }

        protected void SendAsync(string data, Action<bool> completed)
        {
            if (_websocket != null)
                _websocket.SendAsync(data, completed);
        }

        protected void SendAsync(Stream stream, int length, Action<bool> completed)
        {
            if (_websocket != null)
                _websocket.SendAsync(stream, length, completed);
        }
    }
}
