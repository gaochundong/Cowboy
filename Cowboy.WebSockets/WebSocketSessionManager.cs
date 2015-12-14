//using System;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;

//namespace Cowboy.WebSockets
//{
//    public class WebSocketSessionManager
//    {
//        private volatile bool _clean;
//        private object _forSweep;
//        private Logger _logger;
//        private Dictionary<string, IWebSocketSession> _sessions;
//        private volatile ServerState _state;
//        private volatile bool _sweeping;
//        private System.Timers.Timer _sweepTimer;
//        private object _sync;
//        private TimeSpan _waitTime;

//        internal WebSocketSessionManager()
//          : this(new Logger())
//        {
//        }

//        internal WebSocketSessionManager(Logger logger)
//        {
//            _logger = logger;

//            _clean = true;
//            _forSweep = new object();
//            _sessions = new Dictionary<string, IWebSocketSession>();
//            _state = ServerState.Ready;
//            _sync = ((ICollection)_sessions).SyncRoot;
//            _waitTime = TimeSpan.FromSeconds(1);

//            setSweepTimer(60000);
//        }

//        internal ServerState State
//        {
//            get
//            {
//                return _state;
//            }
//        }

//        public IEnumerable<string> ActiveIDs
//        {
//            get
//            {
//                foreach (var res in Broadping(WebSocketFrame.EmptyPingBytes, _waitTime))
//                    if (res.Value)
//                        yield return res.Key;
//            }
//        }

//        public int Count
//        {
//            get
//            {
//                lock (_sync)
//                  return _sessions.Count;
//            }
//        }

//        public IEnumerable<string> IDs
//        {
//            get
//            {
//                if (_state == ServerState.ShuttingDown)
//                    return new string[0];

//                lock (_sync)
//                  return _sessions.Keys.ToList();
//            }
//        }

//        public IEnumerable<string> InactiveIDs
//        {
//            get
//            {
//                foreach (var res in Broadping(WebSocketFrame.EmptyPingBytes, _waitTime))
//                    if (!res.Value)
//                        yield return res.Key;
//            }
//        }

//        public IWebSocketSession this[string id]
//        {
//            get
//            {
//                IWebSocketSession session;
//                TryGetSession(id, out session);

//                return session;
//            }
//        }

//        public bool KeepClean
//        {
//            get
//            {
//                return _clean;
//            }

//            internal set
//            {
//                if (!(value ^ _clean))
//                    return;

//                _clean = value;
//                if (_state == ServerState.Start)
//                    _sweepTimer.Enabled = value;
//            }
//        }

//        public IEnumerable<IWebSocketSession> Sessions
//        {
//            get
//            {
//                if (_state == ServerState.ShuttingDown)
//                    return new IWebSocketSession[0];

//                lock (_sync)
//                  return _sessions.Values.ToList();
//            }
//        }

//        public TimeSpan WaitTime
//        {
//            get
//            {
//                return _waitTime;
//            }

//            internal set
//            {
//                if (value == _waitTime)
//                    return;

//                _waitTime = value;
//                foreach (var session in Sessions)
//                    session.Context.WebSocket.WaitTime = value;
//            }
//        }

//        private void broadcast(Opcode opcode, byte[] data, Action completed)
//        {
//            var cache = new Dictionary<CompressionMethod, byte[]>();
//            try
//            {
//                Broadcast(opcode, data, cache);
//                if (completed != null)
//                    completed();
//            }
//            catch (Exception ex)
//            {
//                _logger.Fatal(ex.ToString());
//            }
//            finally
//            {
//                cache.Clear();
//            }
//        }

//        private void broadcast(Opcode opcode, Stream stream, Action completed)
//        {
//            var cache = new Dictionary<CompressionMethod, Stream>();
//            try
//            {
//                Broadcast(opcode, stream, cache);
//                if (completed != null)
//                    completed();
//            }
//            catch (Exception ex)
//            {
//                _logger.Fatal(ex.ToString());
//            }
//            finally
//            {
//                foreach (var cached in cache.Values)
//                    cached.Dispose();

//                cache.Clear();
//            }
//        }

//        private void broadcastAsync(Opcode opcode, byte[] data, Action completed)
//        {
//            ThreadPool.QueueUserWorkItem(state => broadcast(opcode, data, completed));
//        }

//        private void broadcastAsync(Opcode opcode, Stream stream, Action completed)
//        {
//            ThreadPool.QueueUserWorkItem(state => broadcast(opcode, stream, completed));
//        }

//        private static string createID()
//        {
//            return Guid.NewGuid().ToString("N");
//        }

//        private void setSweepTimer(double interval)
//        {
//            _sweepTimer = new System.Timers.Timer(interval);
//            _sweepTimer.Elapsed += (sender, e) => Sweep();
//        }

//        private bool tryGetSession(string id, out IWebSocketSession session)
//        {
//            bool ret;
//            lock (_sync)
//              ret = _sessions.TryGetValue(id, out session);

//            if (!ret)
//                _logger.Error("A session with the specified ID isn't found:\n  ID: " + id);

//            return ret;
//        }

//        internal string Add(IWebSocketSession session)
//        {
//            lock (_sync)
//            {
//                if (_state != ServerState.Start)
//                    return null;

//                var id = createID();
//                _sessions.Add(id, session);

//                return id;
//            }
//        }

//        internal void Broadcast(Opcode opcode, byte[] data, Dictionary<CompressionMethod, byte[]> cache)
//        {
//            foreach (var session in Sessions)
//            {
//                if (_state != ServerState.Start)
//                    break;

//                session.Context.WebSocket.Send(opcode, data, cache);
//            }
//        }

//        internal void Broadcast(Opcode opcode, Stream stream, Dictionary<CompressionMethod, Stream> cache)
//        {
//            foreach (var session in Sessions)
//            {
//                if (_state != ServerState.Start)
//                    break;

//                session.Context.WebSocket.Send(opcode, stream, cache);
//            }
//        }

//        internal Dictionary<string, bool> Broadping(byte[] frameAsBytes, TimeSpan timeout)
//        {
//            var ret = new Dictionary<string, bool>();
//            foreach (var session in Sessions)
//            {
//                if (_state != ServerState.Start)
//                    break;

//                ret.Add(session.ID, session.Context.WebSocket.Ping(frameAsBytes, timeout));
//            }

//            return ret;
//        }

//        internal bool Remove(string id)
//        {
//            lock (_sync)
//              return _sessions.Remove(id);
//        }

//        internal void Start()
//        {
//            lock (_sync)
//            {
//                _sweepTimer.Enabled = _clean;
//                _state = ServerState.Start;
//            }
//        }

//        internal void Stop(CloseEventArgs e, byte[] frameAsBytes, bool receive)
//        {
//            lock (_sync)
//            {
//                _state = ServerState.ShuttingDown;

//                _sweepTimer.Enabled = false;
//                foreach (var session in _sessions.Values.ToList())
//                    session.Context.WebSocket.Close(e, frameAsBytes, receive);

//                _state = ServerState.Stop;
//            }
//        }

//        public void Broadcast(byte[] data)
//        {
//            var msg = _state.CheckIfAvailable(false, true, false) ??
//                      WebSocket.CheckSendParameter(data);

//            if (msg != null)
//            {
//                _logger.Error(msg);
//                return;
//            }

//            if (data.LongLength <= WebSocket.FragmentLength)
//                broadcast(Opcode.Binary, data, null);
//            else
//                broadcast(Opcode.Binary, new MemoryStream(data), null);
//        }

//        public void Broadcast(string data)
//        {
//            var msg = _state.CheckIfAvailable(false, true, false) ??
//                      WebSocket.CheckSendParameter(data);

//            if (msg != null)
//            {
//                _logger.Error(msg);
//                return;
//            }

//            var bytes = data.UTF8Encode();
//            if (bytes.LongLength <= WebSocket.FragmentLength)
//                broadcast(Opcode.Text, bytes, null);
//            else
//                broadcast(Opcode.Text, new MemoryStream(bytes), null);
//        }

//        public void BroadcastAsync(byte[] data, Action completed)
//        {
//            var msg = _state.CheckIfAvailable(false, true, false) ??
//                      WebSocket.CheckSendParameter(data);

//            if (msg != null)
//            {
//                _logger.Error(msg);
//                return;
//            }

//            if (data.LongLength <= WebSocket.FragmentLength)
//                broadcastAsync(Opcode.Binary, data, completed);
//            else
//                broadcastAsync(Opcode.Binary, new MemoryStream(data), completed);
//        }

//        public void BroadcastAsync(string data, Action completed)
//        {
//            var msg = _state.CheckIfAvailable(false, true, false) ??
//                      WebSocket.CheckSendParameter(data);

//            if (msg != null)
//            {
//                _logger.Error(msg);
//                return;
//            }

//            var bytes = data.UTF8Encode();
//            if (bytes.LongLength <= WebSocket.FragmentLength)
//                broadcastAsync(Opcode.Text, bytes, completed);
//            else
//                broadcastAsync(Opcode.Text, new MemoryStream(bytes), completed);
//        }

//        public void BroadcastAsync(Stream stream, int length, Action completed)
//        {
//            var msg = _state.CheckIfAvailable(false, true, false) ??
//                      WebSocket.CheckSendParameters(stream, length);

//            if (msg != null)
//            {
//                _logger.Error(msg);
//                return;
//            }

//            stream.ReadBytesAsync(
//              length,
//              data =>
//              {
//                  var len = data.Length;
//                  if (len == 0)
//                  {
//                      _logger.Error("The data cannot be read from 'stream'.");
//                      return;
//                  }

//                  if (len < length)
//                      _logger.Warn(
//                  String.Format(
//                    "The data with 'length' cannot be read from 'stream':\n  expected: {0}\n  actual: {1}",
//                    length,
//                    len));

//                  if (len <= WebSocket.FragmentLength)
//                      broadcast(Opcode.Binary, data, completed);
//                  else
//                      broadcast(Opcode.Binary, new MemoryStream(data), completed);
//              },
//              ex => _logger.Fatal(ex.ToString()));
//        }

//        public Dictionary<string, bool> Broadping()
//        {
//            var msg = _state.CheckIfAvailable(false, true, false);
//            if (msg != null)
//            {
//                _logger.Error(msg);
//                return null;
//            }

//            return Broadping(WebSocketFrame.EmptyPingBytes, _waitTime);
//        }

//        public Dictionary<string, bool> Broadping(string message)
//        {
//            if (message == null || message.Length == 0)
//                return Broadping();

//            byte[] data = null;
//            var msg = _state.CheckIfAvailable(false, true, false) ??
//                      WebSocket.CheckPingParameter(message, out data);

//            if (msg != null)
//            {
//                _logger.Error(msg);
//                return null;
//            }

//            return Broadping(WebSocketFrame.CreatePingFrame(data, false).ToArray(), _waitTime);
//        }

//        public void CloseSession(string id)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.Close();
//        }

//        public void CloseSession(string id, ushort code, string reason)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.Close(code, reason);
//        }

//        public void CloseSession(string id, CloseStatusCode code, string reason)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.Close(code, reason);
//        }

//        public bool PingTo(string id)
//        {
//            IWebSocketSession session;
//            return TryGetSession(id, out session) && session.Context.WebSocket.Ping();
//        }

//        public bool PingTo(string message, string id)
//        {
//            IWebSocketSession session;
//            return TryGetSession(id, out session) && session.Context.WebSocket.Ping(message);
//        }

//        public void SendTo(byte[] data, string id)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.Send(data);
//        }

//        public void SendTo(string data, string id)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.Send(data);
//        }

//        public void SendToAsync(byte[] data, string id, Action<bool> completed)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.SendAsync(data, completed);
//        }

//        public void SendToAsync(string data, string id, Action<bool> completed)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.SendAsync(data, completed);
//        }

//        public void SendToAsync(Stream stream, int length, string id, Action<bool> completed)
//        {
//            IWebSocketSession session;
//            if (TryGetSession(id, out session))
//                session.Context.WebSocket.SendAsync(stream, length, completed);
//        }

//        public void Sweep()
//        {
//            if (_state != ServerState.Start || _sweeping || Count == 0)
//                return;

//            lock (_forSweep)
//            {
//                _sweeping = true;
//                foreach (var id in InactiveIDs)
//                {
//                    if (_state != ServerState.Start)
//                        break;

//                    lock (_sync)
//                    {
//                        IWebSocketSession session;
//                        if (_sessions.TryGetValue(id, out session))
//                        {
//                            var state = session.State;
//                            if (state == WebSocketState.Open)
//                                session.Context.WebSocket.Close(CloseStatusCode.ProtocolError);
//                            else if (state == WebSocketState.Closing)
//                                continue;
//                            else
//                                _sessions.Remove(id);
//                        }
//                    }
//                }

//                _sweeping = false;
//            }
//        }

//        public bool TryGetSession(string id, out IWebSocketSession session)
//        {
//            var msg = _state.CheckIfAvailable(false, true, false) ?? id.CheckIfValidSessionID();
//            if (msg != null)
//            {
//                _logger.Error(msg);
//                session = null;

//                return false;
//            }

//            return tryGetSession(id, out session);
//        }
//    }
//}
