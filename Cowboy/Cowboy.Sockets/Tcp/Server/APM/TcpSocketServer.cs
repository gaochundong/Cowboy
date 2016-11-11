using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cowboy.Buffer;
using Logrila.Logging;

namespace Cowboy.Sockets
{
    public class TcpSocketServer
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketServer>();
        private TcpListener _listener;
        private readonly ConcurrentDictionary<string, TcpSocketSession> _sessions = new ConcurrentDictionary<string, TcpSocketSession>();
        private readonly TcpSocketServerConfiguration _configuration;
        private readonly object _opsLock = new object();
        private bool _isListening = false;

        #endregion

        #region Constructors

        public TcpSocketServer(int listenedPort, TcpSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, configuration)
        {
        }

        public TcpSocketServer(IPAddress listenedAddress, int listenedPort, TcpSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), configuration)
        {
        }

        public TcpSocketServer(IPEndPoint listenedEndPoint, TcpSocketServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");

            this.ListenedEndPoint = listenedEndPoint;
            _configuration = configuration ?? new TcpSocketServerConfiguration();

            if (_configuration.BufferManager == null)
                throw new InvalidProgramException("The buffer manager in configuration cannot be null.");
            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool IsListening { get { return _isListening; } }
        public int SessionCount { get { return _sessions.Count; } }

        #endregion

        #region Server

        public void Listen()
        {
            lock (_opsLock)
            {
                if (_isListening)
                    return;

                _listener = new TcpListener(this.ListenedEndPoint);
                SetSocketOptions();

                _isListening = true;
                _listener.Start(_configuration.PendingConnectionBacklog);

                ContinueAcceptSession(_listener);
            }
        }

        public void Shutdown()
        {
            lock (_opsLock)
            {
                if (!_isListening)
                    return;

                try
                {
                    _isListening = false;
                    _listener.Stop();

                    foreach (var session in _sessions.Values)
                    {
                        CloseSession(session);
                    }
                    _sessions.Clear();
                    _listener = null;
                }
                catch (Exception ex)
                {
                    if (!ShouldThrow(ex))
                    {
                        _log.Error(ex.Message, ex);
                    }
                    else throw;
                }
            }
        }

        public bool Pending()
        {
            lock (_opsLock)
            {
                if (!_isListening)
                    throw new InvalidOperationException("The server is not listening.");

                // determine if there are pending connection requests.
                return _listener.Pending();
            }
        }

        private void SetSocketOptions()
        {
            _listener.AllowNatTraversal(_configuration.AllowNatTraversal);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);
        }

        private void ContinueAcceptSession(TcpListener listener)
        {
            try
            {
                listener.BeginAcceptTcpClient(new AsyncCallback(HandleTcpClientAccepted), listener);
            }
            catch (Exception ex)
            {
                if (!ShouldThrow(ex))
                {
                    _log.Error(ex.Message, ex);
                }
                else throw;
            }
        }

        private void HandleTcpClientAccepted(IAsyncResult ar)
        {
            if (!_isListening)
                return;

            try
            {
                TcpListener listener = (TcpListener)ar.AsyncState;

                TcpClient tcpClient = listener.EndAcceptTcpClient(ar);
                if (!tcpClient.Connected)
                    return;

                var session = new TcpSocketSession(tcpClient, _configuration, _configuration.BufferManager, this);
                bool isSessionStarted = false;
                try
                {
                    _sessions.AddOrUpdate(session.SessionKey, session, (n, o) => { return o; });
                    session.Start();
                    isSessionStarted = true;
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message, ex);
                }

                if (isSessionStarted)
                {
                    ContinueAcceptSession(listener);
                }
                else
                {
                    CloseSession(session);
                }
            }
            catch (Exception ex)
            {
                if (!ShouldThrow(ex))
                {
                    _log.Error(ex.Message, ex);
                }
                else throw;
            }
        }

        private void CloseSession(TcpSocketSession session)
        {
            TcpSocketSession sessionToBeThrowAway;
            _sessions.TryRemove(session.SessionKey, out sessionToBeThrowAway);

            if (session != null)
            {
                session.Close();
            }
        }

        private bool ShouldThrow(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException)
            {
                return false;
            }
            return false;
        }

        #endregion

        #region Send

        private void GuardRunning()
        {
            if (!_isListening)
                throw new InvalidProgramException("This tcp server has not been started yet.");
        }

        public void SendTo(string sessionKey, byte[] data)
        {
            GuardRunning();

            if (string.IsNullOrEmpty(sessionKey))
                throw new ArgumentNullException("sessionKey");

            if (data == null)
                throw new ArgumentNullException("data");

            SendTo(sessionKey, data, 0, data.Length);
        }

        public void SendTo(string sessionKey, byte[] data, int offset, int count)
        {
            GuardRunning();

            if (string.IsNullOrEmpty(sessionKey))
                throw new ArgumentNullException("sessionKey");

            if (data == null)
                throw new ArgumentNullException("data");

            TcpSocketSession session = null;
            if (_sessions.TryGetValue(sessionKey, out session))
            {
                session.Send(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", sessionKey);
            }
        }

        public void SendTo(TcpSocketSession session, byte[] data)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            if (data == null)
                throw new ArgumentNullException("data");

            SendTo(session, data, 0, data.Length);
        }

        public void SendTo(TcpSocketSession session, byte[] data, int offset, int count)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            if (data == null)
                throw new ArgumentNullException("data");

            TcpSocketSession writeSession = null;
            if (_sessions.TryGetValue(session.SessionKey, out writeSession))
            {
                session.Send(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", session);
            }
        }

        public void BeginSendTo(string sessionKey, byte[] data)
        {
            GuardRunning();

            if (string.IsNullOrEmpty(sessionKey))
                throw new ArgumentNullException("sessionKey");

            if (data == null)
                throw new ArgumentNullException("data");

            BeginSendTo(sessionKey, data, 0, data.Length);
        }

        public void BeginSendTo(string sessionKey, byte[] data, int offset, int count)
        {
            GuardRunning();

            if (string.IsNullOrEmpty(sessionKey))
                throw new ArgumentNullException("sessionKey");

            if (data == null)
                throw new ArgumentNullException("data");

            TcpSocketSession session = null;
            if (_sessions.TryGetValue(sessionKey, out session))
            {
                session.BeginSend(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", sessionKey);
            }
        }

        public void BeginSendTo(TcpSocketSession session, byte[] data)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            if (data == null)
                throw new ArgumentNullException("data");

            BeginSendTo(session, data, 0, data.Length);
        }

        public void BeginSendTo(TcpSocketSession session, byte[] data, int offset, int count)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            if (data == null)
                throw new ArgumentNullException("data");

            TcpSocketSession writeSession = null;
            if (_sessions.TryGetValue(session.SessionKey, out writeSession))
            {
                session.BeginSend(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", session);
            }
        }

        public IAsyncResult BeginSendTo(string sessionKey, byte[] data, AsyncCallback callback, object state)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            return BeginSendTo(sessionKey, data, 0, data.Length, callback, state);
        }

        public IAsyncResult BeginSendTo(string sessionKey, byte[] data, int offset, int count, AsyncCallback callback, object state)
        {
            GuardRunning();

            if (string.IsNullOrEmpty(sessionKey))
                throw new ArgumentNullException("sessionKey");

            if (data == null)
                throw new ArgumentNullException("data");

            TcpSocketSession session = null;
            if (_sessions.TryGetValue(sessionKey, out session))
            {
                return session.BeginSend(data, offset, count, callback, state);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", sessionKey);
            }

            return null;
        }

        public IAsyncResult BeginSendTo(TcpSocketSession session, byte[] data, AsyncCallback callback, object state)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            if (data == null)
                throw new ArgumentNullException("data");

            return BeginSendTo(session, data, 0, data.Length, callback, state);
        }

        public IAsyncResult BeginSendTo(TcpSocketSession session, byte[] data, int offset, int count, AsyncCallback callback, object state)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            if (data == null)
                throw new ArgumentNullException("data");

            TcpSocketSession writeSession = null;
            if (_sessions.TryGetValue(session.SessionKey, out writeSession))
            {
                return session.BeginSend(data, offset, count, callback, state);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", session);
            }

            return null;
        }

        public void EndSendTo(string sessionKey, IAsyncResult asyncResult)
        {
            GuardRunning();

            if (string.IsNullOrEmpty(sessionKey))
                throw new ArgumentNullException("sessionKey");

            TcpSocketSession session = null;
            if (_sessions.TryGetValue(sessionKey, out session))
            {
                session.EndSend(asyncResult);
            }
        }

        public void EndSendTo(TcpSocketSession session, IAsyncResult asyncResult)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            TcpSocketSession writeSession = null;
            if (_sessions.TryGetValue(session.SessionKey, out writeSession))
            {
                session.EndSend(asyncResult);
            }
        }

        public void Broadcast(byte[] data)
        {
            GuardRunning();

            if (data == null)
                throw new ArgumentNullException("data");

            Broadcast(data, 0, data.Length);
        }

        public void Broadcast(byte[] data, int offset, int count)
        {
            GuardRunning();

            if (data == null)
                throw new ArgumentNullException("data");

            foreach (var session in _sessions.Values)
            {
                session.Send(data, offset, count);
            }
        }

        public void BeginBroadcast(byte[] data)
        {
            GuardRunning();

            if (data == null)
                throw new ArgumentNullException("data");

            BeginBroadcast(data, 0, data.Length);
        }

        public void BeginBroadcast(byte[] data, int offset, int count)
        {
            GuardRunning();

            if (data == null)
                throw new ArgumentNullException("data");

            foreach (var session in _sessions.Values)
            {
                session.BeginSend(data, offset, count);
            }
        }

        #endregion

        #region Session

        public bool HasSession(string sessionKey)
        {
            return _sessions.ContainsKey(sessionKey);
        }

        public TcpSocketSession GetSession(string sessionKey)
        {
            TcpSocketSession session = null;
            _sessions.TryGetValue(sessionKey, out session);
            return session;
        }

        public void CloseSession(string sessionKey)
        {
            TcpSocketSession session = null;
            if (_sessions.TryGetValue(sessionKey, out session))
            {
                session.Close();
            }
        }

        #endregion

        #region Events

        public event EventHandler<TcpClientConnectedEventArgs> ClientConnected;
        public event EventHandler<TcpClientDisconnectedEventArgs> ClientDisconnected;
        public event EventHandler<TcpClientDataReceivedEventArgs> ClientDataReceived;

        internal void RaiseClientConnected(TcpSocketSession session)
        {
            try
            {
                if (ClientConnected != null)
                {
                    ClientConnected(this, new TcpClientConnectedEventArgs(session));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(session, ex);
            }
        }

        internal void RaiseClientDisconnected(TcpSocketSession session)
        {
            try
            {
                if (ClientDisconnected != null)
                {
                    ClientDisconnected(this, new TcpClientDisconnectedEventArgs(session));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(session, ex);
            }
            finally
            {
                TcpSocketSession sessionToBeThrowAway;
                _sessions.TryRemove(session.SessionKey, out sessionToBeThrowAway);
            }
        }

        internal void RaiseClientDataReceived(TcpSocketSession session, byte[] data, int dataOffset, int dataLength)
        {
            try
            {
                if (ClientDataReceived != null)
                {
                    ClientDataReceived(this, new TcpClientDataReceivedEventArgs(session, data, dataOffset, dataLength));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(session, ex);
            }
        }

        private void HandleUserSideError(TcpSocketSession session, Exception ex)
        {
            _log.Error(string.Format("Session [{0}] error occurred in user side [{1}].", session, ex.Message), ex);
        }

        #endregion
    }
}
