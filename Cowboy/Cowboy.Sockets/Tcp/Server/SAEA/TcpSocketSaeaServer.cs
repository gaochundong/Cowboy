using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.Sockets.Buffer;

namespace Cowboy.Sockets
{
    public class TcpSocketSaeaServer : IDisposable
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketSaeaServer>();
        private static readonly byte[] EmptyArray = new byte[0];
        private IBufferManager _bufferManager;
        private readonly ConcurrentDictionary<string, TcpSocketSaeaSession> _sessions = new ConcurrentDictionary<string, TcpSocketSaeaSession>();
        private readonly TcpSocketSaeaServerConfiguration _configuration;
        private readonly ITcpSocketSaeaServerMessageDispatcher _dispatcher;

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;

        private Socket _listener;
        private SaeaPool _acceptSaeaPool;
        private SaeaPool _handleSaeaPool;
        private SessionPool _sessionPool;

        #endregion

        #region Constructors

        public TcpSocketSaeaServer(int listenedPort, ITcpSocketSaeaServerMessageDispatcher dispatcher, TcpSocketSaeaServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, dispatcher, configuration)
        {
        }

        public TcpSocketSaeaServer(IPAddress listenedAddress, int listenedPort, ITcpSocketSaeaServerMessageDispatcher dispatcher, TcpSocketSaeaServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), dispatcher, configuration)
        {
        }

        public TcpSocketSaeaServer(IPEndPoint listenedEndPoint, ITcpSocketSaeaServerMessageDispatcher dispatcher, TcpSocketSaeaServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            this.ListenedEndPoint = listenedEndPoint;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new TcpSocketSaeaServerConfiguration();

            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");

            Initialize();
        }

        public TcpSocketSaeaServer(
            int listenedPort,
            Func<TcpSocketSaeaSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketSaeaSession, Task> onSessionStarted = null,
            Func<TcpSocketSaeaSession, Task> onSessionClosed = null,
            TcpSocketSaeaServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public TcpSocketSaeaServer(
            IPAddress listenedAddress, int listenedPort,
            Func<TcpSocketSaeaSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketSaeaSession, Task> onSessionStarted = null,
            Func<TcpSocketSaeaSession, Task> onSessionClosed = null,
            TcpSocketSaeaServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public TcpSocketSaeaServer(
            IPEndPoint listenedEndPoint,
            Func<TcpSocketSaeaSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketSaeaSession, Task> onSessionStarted = null,
            Func<TcpSocketSaeaSession, Task> onSessionClosed = null,
            TcpSocketSaeaServerConfiguration configuration = null)
            : this(listenedEndPoint,
                  new InternalTcpSocketSaeaServerMessageDispatcherImplementation(onSessionDataReceived, onSessionStarted, onSessionClosed),
                  configuration)
        {
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialPooledBufferCount, _configuration.ReceiveBufferSize);

            _acceptSaeaPool = new SaeaPool(16, 32,
                () =>
                {
                    var saea = new SaeaAwaitable();
                    return saea;
                },
                (saea) =>
                {
                    try
                    {
                        saea.Saea.AcceptSocket = null;
                        saea.Saea.SetBuffer(0, 0);
                        saea.Saea.RemoteEndPoint = null;
                        saea.Saea.SocketFlags = SocketFlags.None;
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex.Message, ex);
                    }
                });
            _handleSaeaPool = new SaeaPool(1024, int.MaxValue,
                () =>
                {
                    var saea = new SaeaAwaitable();
                    return saea;
                },
                (saea) =>
                {
                    try
                    {
                        saea.Saea.AcceptSocket = null;
                        saea.Saea.SetBuffer(EmptyArray, 0, 0);
                        saea.Saea.RemoteEndPoint = null;
                        saea.Saea.SocketFlags = SocketFlags.None;
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex.Message, ex);
                    }
                });
            _sessionPool = new SessionPool(1024, int.MaxValue,
                () =>
                {
                    var session = new TcpSocketSaeaSession(_configuration, _bufferManager, _handleSaeaPool, _dispatcher, this);
                    return session;
                },
                (session) =>
                {
                    try
                    {
                        session.Clear();
                    }
                    catch (Exception ex)
                    {
                        _log.Error(ex.Message, ex);
                    }
                });
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool IsListening { get { return _state == _listening; } }
        public int SessionCount { get { return _sessions.Count; } }

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
                throw new InvalidOperationException("This tcp server has already started.");
            }

            try
            {
                _listener = new Socket(this.ListenedEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                _listener.Bind(this.ListenedEndPoint);

                ConfigureListener();

                _listener.Listen(_configuration.PendingConnectionBacklog);

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
                _listener.Close(0);
                _listener = null;

                Task.Run(async () =>
                {
                    try
                    {
                        foreach (var session in _sessions.Values)
                        {
                            await session.Close();
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
            AllowNatTraversal(_configuration.AllowNatTraversal);
        }

        private void AllowNatTraversal(bool allowed)
        {
            if (allowed)
            {
                _listener.SetIPProtectionLevel(IPProtectionLevel.Unrestricted);
            }
            else
            {
                _listener.SetIPProtectionLevel(IPProtectionLevel.EdgeRestricted);
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

        private async Task Accept()
        {
            try
            {
                while (IsListening)
                {
                    var saea = _acceptSaeaPool.Take();

                    var socketError = await _listener.AcceptAsync(saea);
                    if (socketError == SocketError.Success)
                    {
                        var acceptedSocket = saea.Saea.AcceptSocket;
                        Task.Run(async () =>
                        {
                            await Process(acceptedSocket);
                        })
                        .Forget();
                    }
                    else
                    {
                        _log.ErrorFormat("Error occurred when accept incoming socket [{0}].", socketError);
                    }

                    _acceptSaeaPool.Return(saea);
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
            }
        }

        private async Task Process(Socket acceptedSocket)
        {
            var session = _sessionPool.Take();
            session.Attach(acceptedSocket);

            if (_sessions.TryAdd(session.SessionKey, session))
            {
                _log.DebugFormat("New session [{0}].", session);
                try
                {
                    await session.Start();
                }
                finally
                {
                    TcpSocketSaeaSession recycle;
                    if (_sessions.TryRemove(session.SessionKey, out recycle))
                    {
                        _log.DebugFormat("Close session [{0}].", recycle);
                    }
                }
            }

            _sessionPool.Return(session);
        }

        #endregion

        #region Send

        public async Task SendToAsync(string sessionKey, byte[] data)
        {
            await SendToAsync(sessionKey, data, 0, data.Length);
        }

        public async Task SendToAsync(string sessionKey, byte[] data, int offset, int count)
        {
            TcpSocketSaeaSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendAsync(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendToAsync(TcpSocketSaeaSession session, byte[] data)
        {
            await SendToAsync(session, data, 0, data.Length);
        }

        public async Task SendToAsync(TcpSocketSaeaSession session, byte[] data, int offset, int count)
        {
            TcpSocketSaeaSession sessionFound;
            if (_sessions.TryGetValue(session.SessionKey, out sessionFound))
            {
                await sessionFound.SendAsync(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", session);
            }
        }

        public async Task BroadcastAsync(byte[] data)
        {
            await BroadcastAsync(data, 0, data.Length);
        }

        public async Task BroadcastAsync(byte[] data, int offset, int count)
        {
            foreach (var session in _sessions.Values)
            {
                await session.SendAsync(data, offset, count);
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Shutdown();
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message, ex);
                }
            }
        }

        #endregion
    }
}
