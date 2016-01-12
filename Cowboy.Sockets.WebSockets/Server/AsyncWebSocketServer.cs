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
        private readonly SemaphoreSlim _opsLock = new SemaphoreSlim(1, 1);
        private readonly AsyncWebSocketServerConfiguration _configuration;
        private readonly IAsyncWebSocketServerMessageDispatcher _dispatcher;

        #endregion

        #region Constructors

        public AsyncWebSocketServer(int listenedPort, IAsyncWebSocketServerMessageDispatcher dispatcher, AsyncWebSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, dispatcher, configuration)
        {
        }

        public AsyncWebSocketServer(IPAddress listenedAddress, int listenedPort, IAsyncWebSocketServerMessageDispatcher dispatcher, AsyncWebSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), dispatcher, configuration)
        {
        }

        public AsyncWebSocketServer(IPEndPoint listenedEndPoint, IAsyncWebSocketServerMessageDispatcher dispatcher, AsyncWebSocketServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            this.ListenedEndPoint = listenedEndPoint;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new AsyncWebSocketServerConfiguration();

            Initialize();
        }

        public AsyncWebSocketServer(
            int listenedPort,
            Func<AsyncWebSocketSession, string, Task> onSessionTextReceived = null,
            Func<AsyncWebSocketSession, byte[], int, int, Task> onSessionBinaryReceived = null,
            Func<AsyncWebSocketSession, Task> onSessionStarted = null,
            Func<AsyncWebSocketSession, Task> onSessionClosed = null,
            AsyncWebSocketServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort,
                  onSessionTextReceived, onSessionBinaryReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public AsyncWebSocketServer(
            IPAddress listenedAddress, int listenedPort,
            Func<AsyncWebSocketSession, string, Task> onSessionTextReceived = null,
            Func<AsyncWebSocketSession, byte[], int, int, Task> onSessionBinaryReceived = null,
            Func<AsyncWebSocketSession, Task> onSessionStarted = null,
            Func<AsyncWebSocketSession, Task> onSessionClosed = null,
            AsyncWebSocketServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort),
                  onSessionTextReceived, onSessionBinaryReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public AsyncWebSocketServer(
            IPEndPoint listenedEndPoint,
            Func<AsyncWebSocketSession, string, Task> onSessionTextReceived = null,
            Func<AsyncWebSocketSession, byte[], int, int, Task> onSessionBinaryReceived = null,
            Func<AsyncWebSocketSession, Task> onSessionStarted = null,
            Func<AsyncWebSocketSession, Task> onSessionClosed = null,
            AsyncWebSocketServerConfiguration configuration = null)
            : this(listenedEndPoint,
                  new InternalAsyncWebSocketServerMessageDispatcherImplementation(
                      onSessionTextReceived, onSessionBinaryReceived, onSessionStarted, onSessionClosed),
                  configuration)
        {
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool Active { get; private set; }
        public int SessionCount { get { return _sessions.Count; } }

        #endregion

        #region Server

        public async Task Start()
        {
            if (await _opsLock.WaitAsync(0))
            {
                try
                {
                    if (Active)
                        return;

                    try
                    {
                        _listener = new TcpListener(this.ListenedEndPoint);
                        ConfigureListener();

                        _listener.Start(_configuration.PendingConnectionBacklog);
                        Active = true;

                        Task.Run(async () =>
                        {
                            await Accept();
                        })
                        .Forget();
                    }
                    catch (Exception ex) when (!ShouldThrow(ex)) { }
                }
                finally
                {
                    _opsLock.Release();
                }
            }
        }

        public async Task Stop()
        {
            if (await _opsLock.WaitAsync(0))
            {
                try
                {
                    if (!Active)
                        return;

                    try
                    {
                        Active = false;
                        _listener.Stop();
                        _listener = null;

                        foreach (var session in _sessions.Values)
                        {
                            await session.Close(WebSocketCloseCode.NormalClosure);
                        }
                    }
                    catch (Exception ex) when (!ShouldThrow(ex)) { }
                }
                finally
                {
                    _opsLock.Release();
                }
            }
        }

        private void ConfigureListener()
        {
            _listener.AllowNatTraversal(_configuration.AllowNatTraversal);
        }

        public bool Pending()
        {
            lock (_opsLock)
            {
                if (!Active)
                    throw new InvalidOperationException("The TCP server is not active.");

                // determine if there are pending connection requests.
                return _listener.Pending();
            }
        }

        private async Task Accept()
        {
            try
            {
                while (Active)
                {
                    var tcpClient = await _listener.AcceptTcpClientAsync();
                    var session = new AsyncWebSocketSession(tcpClient, _configuration, _bufferManager, _dispatcher, false, this);
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
