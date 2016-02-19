using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.Sockets.Buffer;

namespace Cowboy.Sockets.Experimental
{
    public class TcpSocketRioServer : IDisposable
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketRioServer>();
        private static readonly byte[] EmptyArray = new byte[0];
        private IBufferManager _bufferManager;
        private readonly ConcurrentDictionary<string, TcpSocketRioSession> _sessions = new ConcurrentDictionary<string, TcpSocketRioSession>();
        private readonly TcpSocketRioServerConfiguration _configuration;
        private readonly ITcpSocketRioServerMessageDispatcher _dispatcher;

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;

        private RioFixedBufferPool _sendPool;
        private RioFixedBufferPool _receivePool;
        private RioTcpListener _listener;

        #endregion

        #region Constructors

        public TcpSocketRioServer(int listenedPort, ITcpSocketRioServerMessageDispatcher dispatcher, TcpSocketRioServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, dispatcher, configuration)
        {
        }

        public TcpSocketRioServer(IPAddress listenedAddress, int listenedPort, ITcpSocketRioServerMessageDispatcher dispatcher, TcpSocketRioServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), dispatcher, configuration)
        {
        }

        public TcpSocketRioServer(IPEndPoint listenedEndPoint, ITcpSocketRioServerMessageDispatcher dispatcher, TcpSocketRioServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            this.ListenedEndPoint = listenedEndPoint;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new TcpSocketRioServerConfiguration();

            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");

            Initialize();
        }

        public TcpSocketRioServer(
            int listenedPort,
            Func<TcpSocketRioSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketRioSession, Task> onSessionStarted = null,
            Func<TcpSocketRioSession, Task> onSessionClosed = null,
            TcpSocketRioServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public TcpSocketRioServer(
            IPAddress listenedAddress, int listenedPort,
            Func<TcpSocketRioSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketRioSession, Task> onSessionStarted = null,
            Func<TcpSocketRioSession, Task> onSessionClosed = null,
            TcpSocketRioServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), onSessionDataReceived, onSessionStarted, onSessionClosed, configuration)
        {
        }

        public TcpSocketRioServer(
            IPEndPoint listenedEndPoint,
            Func<TcpSocketRioSession, byte[], int, int, Task> onSessionDataReceived = null,
            Func<TcpSocketRioSession, Task> onSessionStarted = null,
            Func<TcpSocketRioSession, Task> onSessionClosed = null,
            TcpSocketRioServerConfiguration configuration = null)
            : this(listenedEndPoint,
                  new InternalTcpSocketRioServerMessageDispatcherImplementation(onSessionDataReceived, onSessionStarted, onSessionClosed),
                  configuration)
        {
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialPooledBufferCount, _configuration.ReceiveBufferSize);

            int pipeLineDeph = 16;
            int connections = 1024;

            _sendPool = new RioFixedBufferPool(10 * connections, 140 * pipeLineDeph);
            _receivePool = new RioFixedBufferPool(10 * connections, 128 * pipeLineDeph);

            _listener = new RioTcpListener(_sendPool, _receivePool, 1024);

            _listener.OnAccepted = (acceptedSocket) =>
            {
                Task.Run(async () =>
                {
                    await Process(acceptedSocket);
                })
                .Forget();
            };
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
                _listener.Listen(this.ListenedEndPoint, 1024 * 1024);
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
                _listener.Dispose();
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

                _sendPool.Dispose();
                _receivePool.Dispose();
                _sendPool = null;
                _receivePool = null;
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
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

        private async Task Process(RioConnectionOrientedSocket acceptedSocket)
        {
            var session = new TcpSocketRioSession(_configuration, _bufferManager, acceptedSocket, _dispatcher, this);

            if (_sessions.TryAdd(session.SessionKey, session))
            {
                _log.DebugFormat("New session [{0}].", session);
                try
                {
                    await session.Start();
                }
                finally
                {
                    TcpSocketRioSession recycle;
                    if (_sessions.TryRemove(session.SessionKey, out recycle))
                    {
                        _log.DebugFormat("Close session [{0}].", recycle);
                    }
                }
            }
        }

        #endregion

        #region Send

        public async Task SendToAsync(string sessionKey, byte[] data)
        {
            await SendToAsync(sessionKey, data, 0, data.Length);
        }

        public async Task SendToAsync(string sessionKey, byte[] data, int offset, int count)
        {
            TcpSocketRioSession sessionFound;
            if (_sessions.TryGetValue(sessionKey, out sessionFound))
            {
                await sessionFound.SendAsync(data, offset, count);
            }
            else
            {
                _log.WarnFormat("Cannot find session [{0}].", sessionKey);
            }
        }

        public async Task SendToAsync(TcpSocketRioSession session, byte[] data)
        {
            await SendToAsync(session, data, 0, data.Length);
        }

        public async Task SendToAsync(TcpSocketRioSession session, byte[] data, int offset, int count)
        {
            TcpSocketRioSession sessionFound;
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

        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_sendPool")]
        [SuppressMessage("Microsoft.Usage", "CA2213:DisposableFieldsShouldBeDisposed", MessageId = "_receivePool")]
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
