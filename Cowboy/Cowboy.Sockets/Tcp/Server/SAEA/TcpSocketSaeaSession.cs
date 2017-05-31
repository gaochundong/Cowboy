using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Logrila.Logging;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketSaeaSession
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketSaeaSession>();
        private readonly object _opsLock = new object();
        private readonly TcpSocketSaeaServerConfiguration _configuration;
        private readonly ISegmentBufferManager _bufferManager;
        private readonly SaeaPool _saeaPool;
        private readonly ITcpSocketSaeaServerMessageDispatcher _dispatcher;
        private readonly TcpSocketSaeaServer _server;
        private Socket _socket;
        private string _sessionKey;
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _localEndPoint;
        private ArraySegment<byte> _receiveBuffer = default(ArraySegment<byte>);
        private int _receiveBufferOffset = 0;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _disposed = 5;

        #endregion

        #region Constructors

        public TcpSocketSaeaSession(
            TcpSocketSaeaServerConfiguration configuration,
            ISegmentBufferManager bufferManager,
            SaeaPool saeaPool,
            ITcpSocketSaeaServerMessageDispatcher dispatcher,
            TcpSocketSaeaServer server)
        {
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");
            if (saeaPool == null)
                throw new ArgumentNullException("saeaPool");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");
            if (server == null)
                throw new ArgumentNullException("server");

            _configuration = configuration;
            _bufferManager = bufferManager;
            _saeaPool = saeaPool;
            _dispatcher = dispatcher;
            _server = server;

            if (_receiveBuffer == default(ArraySegment<byte>))
                _receiveBuffer = _bufferManager.BorrowBuffer();
            _receiveBufferOffset = 0;
        }

        #endregion

        #region Properties

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get; private set; }

        private bool Connected { get { return _socket != null && _socket.Connected; } }
        public IPEndPoint RemoteEndPoint { get { return Connected ? (IPEndPoint)_socket.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return Connected ? (IPEndPoint)_socket.LocalEndPoint : _localEndPoint; } }

        public Socket Socket { get { return _socket; } }
        public TcpSocketSaeaServer Server { get { return _server; } }

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
            return string.Format("SessionKey[{0}], RemoteEndPoint[{1}], LocalEndPoint[{2}]",
                this.SessionKey, this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Attach

        internal void Attach(Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            lock (_opsLock)
            {
                _socket = socket;
                SetSocketOptions();

                _sessionKey = Guid.NewGuid().ToString();
                this.StartTime = DateTime.UtcNow;

                _remoteEndPoint = this.RemoteEndPoint;
                _localEndPoint = this.LocalEndPoint;
            }
        }

        internal void Clear()
        {
            lock (_opsLock)
            {
                _socket = null;
                _sessionKey = Guid.Empty.ToString();

                _remoteEndPoint = null;
                _localEndPoint = null;
                _state = _none;
                _receiveBufferOffset = 0;
            }
        }

        private void SetSocketOptions()
        {
            _socket.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            _socket.SendBufferSize = _configuration.SendBufferSize;
            _socket.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
            _socket.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
            _socket.NoDelay = _configuration.NoDelay;
            _socket.LingerState = _configuration.LingerState;

            if (_configuration.KeepAlive)
            {
                _socket.SetSocketOption(
                    SocketOptionLevel.Socket,
                    SocketOptionName.KeepAlive,
                    (int)_configuration.KeepAliveInterval.TotalMilliseconds);
            }

            _socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, _configuration.ReuseAddress);
        }

        #endregion

        #region Process

        internal async Task Start()
        {
            int origin = Interlocked.CompareExchange(ref _state, _connecting, _none);
            if (origin == _disposed)
            {
                throw new ObjectDisposedException("This tcp socket session has been disposed when connecting.");
            }
            else if (origin != _none)
            {
                throw new InvalidOperationException("This tcp socket session is in invalid state when connecting.");
            }

            try
            {
                if (Interlocked.CompareExchange(ref _state, _connected, _connecting) != _connecting)
                {
                    await Close(false); // connected with wrong state
                    throw new ObjectDisposedException("This tcp socket session has been disposed after connected.");
                }

                _log.DebugFormat("Session started for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    this.StartTime.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    _dispatcher.GetType().Name,
                    this.Server.SessionCount);
                bool isErrorOccurredInUserSide = false;
                try
                {
                    await _dispatcher.OnSessionStarted(this);
                }
                catch (Exception ex) // catch all exceptions from out-side
                {
                    isErrorOccurredInUserSide = true;
                    await HandleUserSideError(ex);
                }

                if (!isErrorOccurredInUserSide)
                {
                    await Process();
                }
                else
                {
                    await Close(true); // user side handle tcp connection error occurred
                }
            }
            catch (Exception ex) // catch exceptions then log then re-throw
            {
                _log.Error(string.Format("Session [{0}] exception occurred, [{1}].", this, ex.Message), ex);
                await Close(true); // handle tcp connection error occurred
                throw;
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
                int consumedLength = 0;

                var saea = _saeaPool.Take();
                saea.Saea.SetBuffer(_receiveBuffer.Array, _receiveBuffer.Offset + _receiveBufferOffset, _receiveBuffer.Count - _receiveBufferOffset);

                while (State == TcpSocketConnectionState.Connected)
                {
                    saea.Saea.SetBuffer(_receiveBuffer.Array, _receiveBuffer.Offset + _receiveBufferOffset, _receiveBuffer.Count - _receiveBufferOffset);

                    var socketError = await _socket.ReceiveAsync(saea);
                    if (socketError != SocketError.Success)
                        break;

                    var receiveCount = saea.Saea.BytesTransferred;
                    if (receiveCount == 0)
                        break;

                    SegmentBufferDeflector.ReplaceBuffer(_bufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);
                    consumedLength = 0;

                    while (true)
                    {
                        frameLength = 0;
                        payload = null;
                        payloadOffset = 0;
                        payloadCount = 0;

                        if (_configuration.FrameBuilder.Decoder.TryDecodeFrame(
                            _receiveBuffer.Array,
                            _receiveBuffer.Offset + consumedLength,
                            _receiveBufferOffset - consumedLength,
                            out frameLength, out payload, out payloadOffset, out payloadCount))
                        {
                            try
                            {
                                await _dispatcher.OnSessionDataReceived(this, payload, payloadOffset, payloadCount);
                            }
                            catch (Exception ex) // catch all exceptions from out-side
                            {
                                await HandleUserSideError(ex);
                            }
                            finally
                            {
                                consumedLength += frameLength;
                            }
                        }
                        else
                        {
                            break;
                        }
                    }

                    if (_receiveBuffer != null && _receiveBuffer.Array != null)
                    {
                        SegmentBufferDeflector.ShiftBuffer(_bufferManager, consumedLength, ref _receiveBuffer, ref _receiveBufferOffset);
                    }
                }
            }
            catch (Exception ex)
            {
                await HandleReceiveOperationException(ex);
            }
            finally
            {
                await Close(true); // read async buffer returned, remote notifies closed
            }
        }

        public async Task Close()
        {
            await Close(true); // close by external
        }

        private async Task Close(bool shallNotifyUserSide)
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            Shutdown();

            if (shallNotifyUserSide)
            {
                _log.DebugFormat("Session closed for [{0}] on [{1}] in dispatcher [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    _dispatcher.GetType().Name,
                    this.Server.SessionCount - 1);
                try
                {
                    await _dispatcher.OnSessionClosed(this);
                }
                catch (Exception ex) // catch all exceptions from out-side
                {
                    await HandleUserSideError(ex);
                }
            }

            Clean();
        }

        private void Shutdown()
        {
            try
            {
                // The correct way to shut down the connection (especially if you are in a full-duplex conversation) 
                // is to call socket.Shutdown(SocketShutdown.Send) and give the remote party some time to close 
                // their send channel. This ensures that you receive any pending data instead of slamming the 
                // connection shut. ObjectDisposedException should never be part of the normal application flow.
                _socket.Shutdown(SocketShutdown.Send);
            }
            catch { }
        }

        private void Clean()
        {
            try
            {
                try
                {
                    if (_socket != null)
                    {
                        _socket.Dispose();
                    }
                }
                catch { }
            }
            catch { }
            finally
            {
                _socket = null;
            }

            if (_receiveBuffer != default(ArraySegment<byte>))
                _configuration.BufferManager.ReturnBuffer(_receiveBuffer);
            _receiveBuffer = default(ArraySegment<byte>);
            _receiveBufferOffset = 0;
        }

        #endregion

        #region Exception Handler

        private async Task HandleSendOperationException(Exception ex)
        {
            if (IsSocketTimeOut(ex))
            {
                await CloseIfShould(ex);
                throw new TcpSocketException(ex.Message, new TimeoutException(ex.Message, ex));
            }

            await CloseIfShould(ex);
            throw new TcpSocketException(ex.Message, ex);
        }

        private async Task HandleReceiveOperationException(Exception ex)
        {
            if (IsSocketTimeOut(ex))
            {
                await CloseIfShould(ex);
                throw new TcpSocketException(ex.Message, new TimeoutException(ex.Message, ex));
            }

            await CloseIfShould(ex);
            throw new TcpSocketException(ex.Message, ex);
        }

        private bool IsSocketTimeOut(Exception ex)
        {
            return ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut;
        }

        private async Task<bool> CloseIfShould(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException // buffer array operation
                || ex is ArgumentException      // buffer array operation
                )
            {
                _log.Error(ex.Message, ex);

                await Close(); // intend to close the session

                return true;
            }

            return false;
        }

        private async Task HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Session [{0}] error occurred in user side [{1}].", this, ex.Message), ex);
            await Task.CompletedTask;
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
                throw new InvalidOperationException("This session has not connected.");
            }

            try
            {
                byte[] frameBuffer;
                int frameBufferOffset;
                int frameBufferLength;
                _configuration.FrameBuilder.Encoder.EncodeFrame(data, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

                var saea = _saeaPool.Take();
                saea.Saea.SetBuffer(frameBuffer, frameBufferOffset, frameBufferLength);

                await _socket.SendAsync(saea);

                _saeaPool.Return(saea);
            }
            catch (Exception ex)
            {
                await HandleSendOperationException(ex);
            }
        }

        #endregion
    }
}
