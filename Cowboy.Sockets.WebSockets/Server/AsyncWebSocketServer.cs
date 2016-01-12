using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets.WebSockets
{
    public class AsyncWebSocketServer
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
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);
            _routeResolver = new AsyncWebSocketRouteResolver(_catalog);
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool Active { get { return _state == _listening; } }
        public int SessionCount { get { return _sessions.Count; } }

        #endregion

        #region Server

        public void Start()
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

        public async Task Stop()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
                _listener.Stop();
                _listener = null;

                foreach (var session in _sessions.Values)
                {
                    await session.Close(WebSocketCloseCode.NormalClosure);
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        private void ConfigureListener()
        {
            _listener.AllowNatTraversal(_configuration.AllowNatTraversal);
        }

        public bool Pending()
        {
            if (!Active)
                throw new InvalidOperationException("The websocket server is not active.");

            // determine if there are pending connection requests.
            return _listener.Pending();
        }

        private async Task Accept()
        {
            try
            {
                while (Active)
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var session = new AsyncWebSocketSession(tcpClient, _configuration, _bufferManager, _routeResolver, this);
                    Task.Run(async () =>
                    {
                        await Process(session);
                    })
                    .Forget();
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        private async Task Process(AsyncWebSocketSession session)
        {
            string sessionKey = session.RemoteEndPoint.ToString();
            if (_sessions.TryAdd(sessionKey, session))
            {
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
                    _sessions.TryRemove(sessionKey, out throwAway);
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

        public async Task SendTextTo(string sessionKey, string text)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendText(text);
            }
        }

        public async Task SendTextTo(AsyncWebSocketSession session, string text)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendText(text);
            }
        }

        public async Task SendBinaryTo(string sessionKey, byte[] data)
        {
            await SendBinaryTo(sessionKey, data, 0, data.Length);
        }

        public async Task SendBinaryTo(string sessionKey, byte[] data, int offset, int count)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendBinary(data, offset, count);
            }
        }

        public async Task SendBinaryTo(AsyncWebSocketSession session, byte[] data)
        {
            await SendBinaryTo(session, data, 0, data.Length);
        }

        public async Task SendBinaryTo(AsyncWebSocketSession session, byte[] data, int offset, int count)
        {
            AsyncWebSocketSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendBinary(data, offset, count);
            }
        }

        public async Task BroadcastText(string text)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendText(text);
            }
        }

        public async Task BroadcastBinary(byte[] data)
        {
            await BroadcastBinary(data, 0, data.Length);
        }

        public async Task BroadcastBinary(byte[] data, int offset, int count)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendBinary(data, offset, count);
            }
        }

        #endregion
    }
}
