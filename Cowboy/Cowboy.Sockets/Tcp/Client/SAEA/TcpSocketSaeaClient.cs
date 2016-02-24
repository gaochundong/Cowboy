using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Logging;
using Cowboy.Sockets.Buffer;

namespace Cowboy.Sockets
{
    public class TcpSocketSaeaClient
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketSaeaClient>();
        private static readonly byte[] EmptyArray = new byte[0];
        private IBufferManager _bufferManager;
        private readonly ITcpSocketSaeaClientMessageDispatcher _dispatcher;
        private readonly TcpSocketSaeaClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private Socket _socket;
        private SaeaPool _saeaPool;
        private byte[] _receiveBuffer;
        private int _receiveBufferOffset = 0;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _disposed = 5;

        #endregion

        #region Constructors

        public TcpSocketSaeaClient(IPAddress remoteAddress, int remotePort, IPAddress localAddress, int localPort, ITcpSocketSaeaClientMessageDispatcher dispatcher, TcpSocketSaeaClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), new IPEndPoint(localAddress, localPort), dispatcher, configuration)
        {
        }

        public TcpSocketSaeaClient(IPAddress remoteAddress, int remotePort, IPEndPoint localEP, ITcpSocketSaeaClientMessageDispatcher dispatcher, TcpSocketSaeaClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), localEP, dispatcher, configuration)
        {
        }

        public TcpSocketSaeaClient(IPAddress remoteAddress, int remotePort, ITcpSocketSaeaClientMessageDispatcher dispatcher, TcpSocketSaeaClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), dispatcher, configuration)
        {
        }

        public TcpSocketSaeaClient(IPEndPoint remoteEP, ITcpSocketSaeaClientMessageDispatcher dispatcher, TcpSocketSaeaClientConfiguration configuration = null)
            : this(remoteEP, null, dispatcher, configuration)
        {
        }

        public TcpSocketSaeaClient(IPEndPoint remoteEP, IPEndPoint localEP, ITcpSocketSaeaClientMessageDispatcher dispatcher, TcpSocketSaeaClientConfiguration configuration = null)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            _remoteEndPoint = remoteEP;
            _localEndPoint = localEP;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new TcpSocketSaeaClientConfiguration();

            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");

            Initialize();
        }

        public TcpSocketSaeaClient(IPAddress remoteAddress, int remotePort, IPAddress localAddress, int localPort,
            Func<TcpSocketSaeaClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketSaeaClient, Task> onServerConnected = null,
            Func<TcpSocketSaeaClient, Task> onServerDisconnected = null,
            TcpSocketSaeaClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), new IPEndPoint(localAddress, localPort),
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketSaeaClient(IPAddress remoteAddress, int remotePort, IPEndPoint localEP,
            Func<TcpSocketSaeaClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketSaeaClient, Task> onServerConnected = null,
            Func<TcpSocketSaeaClient, Task> onServerDisconnected = null,
            TcpSocketSaeaClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort), localEP,
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketSaeaClient(IPAddress remoteAddress, int remotePort,
            Func<TcpSocketSaeaClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketSaeaClient, Task> onServerConnected = null,
            Func<TcpSocketSaeaClient, Task> onServerDisconnected = null,
            TcpSocketSaeaClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteAddress, remotePort),
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketSaeaClient(IPEndPoint remoteEP,
            Func<TcpSocketSaeaClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketSaeaClient, Task> onServerConnected = null,
            Func<TcpSocketSaeaClient, Task> onServerDisconnected = null,
            TcpSocketSaeaClientConfiguration configuration = null)
            : this(remoteEP, null,
                  onServerDataReceived, onServerConnected, onServerDisconnected, configuration)
        {
        }

        public TcpSocketSaeaClient(IPEndPoint remoteEP, IPEndPoint localEP,
            Func<TcpSocketSaeaClient, byte[], int, int, Task> onServerDataReceived = null,
            Func<TcpSocketSaeaClient, Task> onServerConnected = null,
            Func<TcpSocketSaeaClient, Task> onServerDisconnected = null,
            TcpSocketSaeaClientConfiguration configuration = null)
            : this(remoteEP, localEP,
                 new InternalTcpSocketSaeaClientMessageDispatcherImplementation(onServerDataReceived, onServerConnected, onServerDisconnected),
                 configuration)
        {
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialPooledBufferCount, _configuration.ReceiveBufferSize);

            _saeaPool = new SaeaPool(1024, int.MaxValue,
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
        }

        #endregion

        #region Properties

        public IPEndPoint RemoteEndPoint { get { return (_socket != null && _socket.Connected) ? (IPEndPoint)_socket.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return (_socket != null && _socket.Connected) ? (IPEndPoint)_socket.LocalEndPoint : null; } }

        public TcpSocketConnectionState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return TcpSocketConnectionState.None;
                    case _connecting:
                        return TcpSocketConnectionState.Connecting;
                    case _connected:
                        return TcpSocketConnectionState.Connected;
                    case _disposed:
                        return TcpSocketConnectionState.Closed;
                    default:
                        return TcpSocketConnectionState.Closed;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("RemoteEndPoint[{0}], LocalEndPoint[{1}]",
                this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Connect

        public async Task Connect()
        {
            int origin = Interlocked.CompareExchange(ref _state, _connecting, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This tcp socket client has already connected to server.");
            }

            try
            {
                _socket = new Socket(SocketType.Stream, ProtocolType.Tcp);
                if (_localEndPoint != null)
                {
                    _socket.Bind(_localEndPoint);
                }

                var saea = _saeaPool.Take();
                saea.Saea.RemoteEndPoint = _remoteEndPoint;

                var socketError = await _socket.ConnectAsync(saea);
                if (socketError != SocketError.Success)
                {
                    throw new SocketException((int)socketError);
                }

                ConfigureSocket();

                _receiveBuffer = _bufferManager.BorrowBuffer();
                _receiveBufferOffset = 0;

                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    throw new ObjectDisposedException(GetType().FullName);
                }

                _log.DebugFormat("Connected to server [{0}] with dispatcher [{1}] on [{2}].",
                    this.RemoteEndPoint,
                    _dispatcher.GetType().Name,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));
                bool isErrorOccurredInUserSide = false;
                try
                {
                    await _dispatcher.OnServerConnected(this);
                }
                catch (Exception ex)
                {
                    isErrorOccurredInUserSide = true;
                    HandleUserSideError(ex);
                }

                if (!isErrorOccurredInUserSide)
                {
                    Task.Run(async () =>
                    {
                        await Process();
                    })
                    .Forget();
                }
                else
                {
                    await Close();
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex)
            when (ex is TimeoutException)
            {
                _log.Error(ex.Message, ex);
                await Close();
            }
        }

        private async Task Process()
        {
            try
            {
                int frameLength;
                byte[] payload;
                int payloadOffset;
                int payloadCount;

                var saea = _saeaPool.Take();
                saea.Saea.SetBuffer(_receiveBuffer, _receiveBufferOffset, _receiveBuffer.Length - _receiveBufferOffset);

                while (State == TcpSocketConnectionState.Connected)
                {
                    saea.Saea.SetBuffer(_receiveBufferOffset, _receiveBuffer.Length - _receiveBufferOffset);

                    var socketError = await _socket.ReceiveAsync(saea);
                    if (socketError != SocketError.Success)
                        break;

                    var receiveCount = saea.Saea.BytesTransferred;
                    if (receiveCount == 0)
                        break;

                    BufferDeflector.ReplaceBuffer(_bufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);

                    while (true)
                    {
                        if (_configuration.FrameBuilder.TryDecodeFrame(_receiveBuffer, _receiveBufferOffset,
                            out frameLength, out payload, out payloadOffset, out payloadCount))
                        {
                            try
                            {
                                await _dispatcher.OnServerDataReceived(this, payload, payloadOffset, payloadCount);
                            }
                            catch (Exception ex)
                            {
                                HandleUserSideError(ex);
                            }
                            finally
                            {
                                try
                                {
                                    BufferDeflector.ShiftBuffer(_bufferManager, frameLength, ref _receiveBuffer, ref _receiveBufferOffset);
                                }
                                catch (ArgumentOutOfRangeException) { }
                            }
                        }
                        else
                        {
                            break;
                        }
                    }
                }
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
            finally
            {
                await Close();
            }
        }

        private bool ShouldThrow(Exception ex)
        {
            if (ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut)
            {
                _log.Error(ex.Message, ex);
                return false;
            }

            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException
                )
            {
                if (ex is SocketException)
                    _log.Error(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message), ex);

                return false;
            }

            _log.Error(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message), ex);
            return true;
        }

        private void ConfigureSocket()
        {
            _socket.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            _socket.SendBufferSize = _configuration.SendBufferSize;
            _socket.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
            _socket.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
            _socket.NoDelay = _configuration.NoDelay;
            _socket.LingerState = _configuration.LingerState;
        }

        private void HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Session [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
        }

        #endregion

        #region Close

        public async Task Close()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
                if (_socket != null && _socket.Connected)
                {
                    _socket.Dispose();
                    _socket = null;
                }
            }
            catch (Exception) { }

            if (_receiveBuffer != null)
                _bufferManager.ReturnBuffer(_receiveBuffer);
            _receiveBufferOffset = 0;

            _log.DebugFormat("Disconnected from server [{0}] with dispatcher [{1}] on [{2}].",
                this.RemoteEndPoint,
                _dispatcher.GetType().Name,
                DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));
            try
            {
                await _dispatcher.OnServerDisconnected(this);
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        #endregion

        #region Send

        public async Task SendAsync(byte[] data)
        {
            await SendAsync(data, 0, data.Length);
        }

        public async Task SendAsync(byte[] data, int offset, int count)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            if (State != TcpSocketConnectionState.Connected)
            {
                throw new InvalidOperationException("This client has not connected to server.");
            }

            try
            {
                var frame = _configuration.FrameBuilder.EncodeFrame(data, offset, count);
                var saea = _saeaPool.Take();
                saea.Saea.SetBuffer(frame, 0, frame.Length);

                await _socket.SendAsync(saea);

                _saeaPool.Return(saea);
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        #endregion
    }
}
