using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.WebSockets.Buffer;

namespace Cowboy.WebSockets
{
    public sealed class AsyncWebSocketServer
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<AsyncWebSocketServer>();
        private IBufferManager _bufferManager;
        private TcpListener _listener;
        private readonly ConcurrentDictionary<string, AsyncWebSocketSession> _sessions = new ConcurrentDictionary<string, AsyncWebSocketSession>();
        private readonly AsyncWebSocketServerModuleCatalog _catalog;
        private readonly AsyncWebSocketServerConfiguration _configuration;
        private AsyncWebSocketRouteResolver _routeResolver;

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;

        #endregion

        #region Constructors

        public AsyncWebSocketServer(int listenedPort, AsyncWebSocketServerModuleCatalog catalog, AsyncWebSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, catalog, configuration)
        {
        }

        public AsyncWebSocketServer(IPAddress listenedAddress, int listenedPort, AsyncWebSocketServerModuleCatalog catalog, AsyncWebSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), catalog, configuration)
        {
        }

        public AsyncWebSocketServer(IPEndPoint listenedEndPoint, AsyncWebSocketServerModuleCatalog catalog, AsyncWebSocketServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");
            if (catalog == null)
                throw new ArgumentNullException("catalog");

            this.ListenedEndPoint = listenedEndPoint;
            _catalog = catalog;
            _configuration = configuration ?? new AsyncWebSocketServerConfiguration();

            Initialize();
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialPooledBufferCount, _configuration.ReceiveBufferSize);
            _routeResolver = new AsyncWebSocketRouteResolver(_catalog);
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool IsListening { get { return _state == _listening; } }
        public int SessionCount { get { return _sessions.Count; } }

        public IEnumerable<string> EnabledExtensions
        {
            get { return _configuration.EnabledExtensions != null ? _configuration.EnabledExtensions.Keys : null; }
        }
        public IEnumerable<string> EnabledSubProtocols
        {
            get { return _configuration.EnabledSubProtocols != null ? _configuration.EnabledSubProtocols.Keys : null; }
        }

        #endregion

        #region Server

        public void Listen()
        {
            int origin = Interlocked.CompareExchange(ref _state, _listening, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This websocket server has already started.");
            }

            try
            {
                _listener = new TcpListener(this.ListenedEndPoint);
                ConfigureListener();

                _listener.Start(_configuration.PendingConnectionBacklog);

                Task.Run(async () =>
                {
                    await Accept();
                })
                .Forget();
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        public void Shutdown()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
                _listener.Stop();
                _listener = null;

                Task.Run(async () =>
                {
                    try
                    {
                        foreach (var session in _sessions.Values)
                        {
                            await session.Close(WebSocketCloseCode.NormalClosure);
                        }
                    }
                    catch (Exception ex) when (!ShouldThrow(ex)) { }
                })
                .Wait();
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        private void ConfigureListener()
        {
            _listener.AllowNatTraversal(_configuration.AllowNatTraversal);
        }

        public bool Pending()
        {
            if (!IsListening)
                throw new InvalidOperationException("The websocket server is not active.");

            // determine if there are pending connection requests.
            return _listener.Pending();
        }

        private async Task Accept()
        {
            try
            {
                while (IsListening)
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    Task.Run(async () =>
                    {
                        await Process(tcpClient);
                    })
                    .Forget();
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
            }
        }

        private async Task Process(TcpClient acceptedTcpClient)
        {
            var session = new AsyncWebSocketSession(acceptedTcpClient, _configuration, _bufferManager, _routeResolver, this);

            if (_sessions.TryAdd(session.SessionKey, session))
            {
                _log.DebugFormat("New session [{0}].", session);
                try
                {
                    await session.Start();
                }
                catch (Exception ex)
                when (ex is TimeoutException || ex is WebSocketException)
                {
                    _log.Error(ex.Message, ex);
                }
                finally
                {
                    AsyncWebSocketSession throwAway;
                    if (_sessions.TryRemove(session.SessionKey, out throwAway))
                    {
                        _log.DebugFormat("Close session [{0}].", throwAway);
                    }
                }
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
            return true;
        }

        #endregion

        #region Send

        public async Task SendTextToAsync(string sessionKey, string text)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendTextAsync(text);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendTextToAsync(AsyncWebSocketSession session, string text)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendTextAsync(text);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", session);
            }
        }

        public async Task SendBinaryToAsync(string sessionKey, byte[] data)
        {
            await SendBinaryToAsync(sessionKey, data, 0, data.Length);
        }

        public async Task SendBinaryToAsync(string sessionKey, byte[] data, int offset, int count)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendBinaryAsync(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendBinaryToAsync(AsyncWebSocketSession session, byte[] data)
        {
            await SendBinaryToAsync(session, data, 0, data.Length);
        }

        public async Task SendBinaryToAsync(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendBinaryAsync(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", session);
            }
        }

        public async Task BroadcastTextAsync(string text)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendTextAsync(text);
            }
        }

        public async Task BroadcastBinaryAsync(byte[] data)
        {
            await BroadcastBinaryAsync(data, 0, data.Length);
        }

        public async Task BroadcastBinaryAsync(byte[] data, int offset, int count)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendBinaryAsync(data, offset, count);
            }
        }

        #endregion
    }
}
