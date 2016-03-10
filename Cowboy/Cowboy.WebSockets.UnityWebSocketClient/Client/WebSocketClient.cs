using System;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Cowboy.Buffer;

namespace Cowboy.WebSockets
{
    public sealed class WebSocketClient : IDisposable
    {
        #region Fields

        private Action<string> _log;
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private readonly WebSocketClientConfiguration _configuration;
        private readonly IFrameBuilder _frameBuilder = new WebSocketFrameBuilder();
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _localEndPoint;
        private Stream _stream;
        private byte[] _receiveBuffer;
        private int _receiveBufferOffset = 0;

        private readonly Uri _uri;
        private bool _sslEnabled = false;
        private string _secWebSocketKey;

        private int _state;
        private const int _none = 0;
        private const int _connecting = 1;
        private const int _connected = 2;
        private const int _closing = 3;
        private const int _disposed = 5;

        private readonly Semaphore _keepAliveLocker = new Semaphore(1, 1);
        private KeepAliveTracker _keepAliveTracker;
        private Timer _keepAliveTimeoutTimer;
        private Timer _closingTimeoutTimer;

        #endregion

        #region Constructors

        public WebSocketClient(Uri uri, WebSocketClientConfiguration configuration = null, Action<string> log = null)
        {
            if (uri == null)
                throw new ArgumentNullException("uri");

            if (!Consts.WebSocketSchemes.Contains(uri.Scheme.ToLowerInvariant()))
                throw new NotSupportedException(
                    string.Format("Not support the specified scheme [{0}].", uri.Scheme));

            _uri = uri;
            _remoteEndPoint = ResolveRemoteEndPoint(_uri);
            _configuration = configuration != null ? configuration : new WebSocketClientConfiguration();
            _log = log != null ? log : (s) => { };
            _sslEnabled = uri.Scheme.ToLowerInvariant() == "wss";

            Initialize();
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialPooledBufferCount, _configuration.ReceiveBufferSize);
            _keepAliveTracker = KeepAliveTracker.Create(KeepAliveInterval, new TimerCallback((s) => OnKeepAlive()));
            _keepAliveTimeoutTimer = new Timer(new TimerCallback((s) => OnKeepAliveTimeout()), null, Timeout.Infinite, Timeout.Infinite);
            _closingTimeoutTimer = new Timer(new TimerCallback((s) => OnCloseTimeout()), null, Timeout.Infinite, Timeout.Infinite);
        }

        private IPEndPoint ResolveRemoteEndPoint(Uri uri)
        {
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : uri.Scheme.ToLowerInvariant() == "wss" ? 443 : 80;

            IPAddress ipAddress;
            if (IPAddress.TryParse(host, out ipAddress))
            {
                return new IPEndPoint(ipAddress, port);
            }
            else
            {
                if (host.ToLowerInvariant() == "localhost")
                {
                    return new IPEndPoint(IPAddress.Parse(@"127.0.0.1"), port);
                }
                else
                {
                    IPAddress[] addresses = Dns.GetHostAddresses(host);
                    if (addresses.Length > 0)
                    {
                        return new IPEndPoint(addresses[0], port);
                    }
                    else
                    {
                        throw new InvalidOperationException(
                            string.Format("Cannot resolve host [{0}] by DNS.", host));
                    }
                }
            }
        }

        #endregion

        #region Properties

        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.RemoteEndPoint : _remoteEndPoint;
            }
        }
        public IPEndPoint LocalEndPoint
        {
            get
            {
                return (_tcpClient != null && _tcpClient.Client.Connected) ?
                    (IPEndPoint)_tcpClient.Client.LocalEndPoint : _localEndPoint;
            }
        }

        public Uri Uri { get { return _uri; } }

        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }
        public TimeSpan CloseTimeout { get { return _configuration.CloseTimeout; } }
        public TimeSpan KeepAliveInterval { get { return _configuration.KeepAliveInterval; } }
        public TimeSpan KeepAliveTimeout { get { return _configuration.KeepAliveTimeout; } }

        public WebSocketState State
        {
            get
            {
                switch (_state)
                {
                    case _none:
                        return WebSocketState.None;
                    case _connecting:
                        return WebSocketState.Connecting;
                    case _connected:
                        return WebSocketState.Open;
                    case _closing:
                        return WebSocketState.Closing;
                    case _disposed:
                        return WebSocketState.Closed;
                    default:
                        return WebSocketState.Closed;
                }
            }
        }

        public override string ToString()
        {
            return string.Format("RemoteEndPoint[{0}], LocalEndPoint[{1}]",
                this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        #region Close

        public void Close(WebSocketCloseCode closeCode)
        {
            Close(closeCode, null);
        }

        public void Close(WebSocketCloseCode closeCode, string closeReason)
        {
            if (State == WebSocketState.Closed || State == WebSocketState.None)
                return;

            var priorState = Interlocked.Exchange(ref _state, _closing);
            switch (priorState)
            {
                case _connected:
                    {
                        var closingHandshake = new CloseFrame(closeCode, closeReason).ToArray(_frameBuilder);
                        try
                        {
                            if (_stream.CanWrite)
                            {
                                _stream.BeginWrite(closingHandshake, 0, closingHandshake.Length, HandleClosingHandshakeDataWritten, _stream);
                                StartClosingTimer();
                            }
                        }
                        catch (Exception ex)
                        {
                            if (ShouldThrow(ex))
                                throw;
                        }
                        return;
                    }
                case _connecting:
                case _closing:
                    {
                        Close();
                        return;
                    }
                case _disposed:
                case _none:
                default:
                    return;
            }
        }

        private void HandleClosingHandshakeDataWritten(IAsyncResult ar)
        {
            try
            {
                _stream.EndWrite(ar);
            }
            catch (Exception) { }
        }

        private void Close()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
                if (_keepAliveTracker != null)
                {
                    _keepAliveTracker.Dispose();
                }
                if (_keepAliveTimeoutTimer != null)
                {
                    _keepAliveTimeoutTimer.Dispose();
                }
                if (_closingTimeoutTimer != null)
                {
                    _closingTimeoutTimer.Dispose();
                }
                if (_stream != null)
                {
                    _stream.Dispose();
                    _stream = null;
                }
                if (_tcpClient != null && _tcpClient.Connected)
                {
                    _tcpClient.Close();
                    _tcpClient = null;
                }
            }
            catch (Exception) { }

            if (_receiveBuffer != null)
                _bufferManager.ReturnBuffer(_receiveBuffer);
            _receiveBufferOffset = 0;

            _log(string.Format("Disconnected from server [{0}] on [{1}].",
                this.RemoteEndPoint, DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff")));
            try
            {
                RaiseServerDisconnected();
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        public void Abort()
        {
            Close();
        }

        private void StartClosingTimer()
        {
            // In abnormal cases (such as not having received a TCP Close 
            // from the server after a reasonable amount of time) a client MAY initiate the TCP Close.
            _closingTimeoutTimer.Change((int)CloseTimeout.TotalMilliseconds, Timeout.Infinite);
        }

        private void OnCloseTimeout()
        {
            // After both sending and receiving a Close message, an endpoint
            // considers the WebSocket connection closed and MUST close the
            // underlying TCP connection.  The server MUST close the underlying TCP
            // connection immediately; the client SHOULD wait for the server to
            // close the connection but MAY close the connection at any time after
            // sending and receiving a Close message, e.g., if it has not received a
            // TCP Close from the server in a reasonable time period.
            _log(string.Format("Closing timer timeout [{0}] then close automatically.", CloseTimeout));
            Close();
        }

        #endregion

        #region Exception Handler

        private bool CloseIfShould(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is SocketException
                || ex is IOException
                || ex is NullReferenceException
                )
            {
                _log(ex.Message);

                Close();

                return true;
            }

            return false;
        }

        private bool ShouldThrow(Exception ex)
        {
            if (ex is IOException
                && ex.InnerException != null
                && ex.InnerException is SocketException
                && (ex.InnerException as SocketException).SocketErrorCode == SocketError.TimedOut)
            {
                _log(ex.Message);
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
                    _log(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message));

                return false;
            }

            _log(string.Format("Client [{0}] exception occurred, [{1}].", this, ex.Message));
            return true;
        }

        private void HandleUserSideError(Exception ex)
        {
            _log(string.Format("Client [{0}] error occurred in user side [{1}].", this, ex.Message));
        }

        #endregion

        #region Events

        public event EventHandler<WebSocketServerConnectedEventArgs> ServerConnected;
        public event EventHandler<WebSocketServerDisconnectedEventArgs> ServerDisconnected;
        public event EventHandler<WebSocketServerDataReceivedEventArgs> ServerDataReceived;

        private void RaiseServerConnected()
        {
            if (ServerConnected != null)
            {
                ServerConnected(this, new WebSocketServerConnectedEventArgs(this.RemoteEndPoint, this.LocalEndPoint));
            }
        }

        private void RaiseServerDisconnected()
        {
            if (ServerDisconnected != null)
            {
                ServerDisconnected(this, new WebSocketServerDisconnectedEventArgs(_remoteEndPoint, _localEndPoint));
            }
        }

        private void RaiseServerDataReceived(byte[] data, int dataOffset, int dataLength)
        {
            if (ServerDataReceived != null)
            {
                ServerDataReceived(this, new WebSocketServerDataReceivedEventArgs(this, data, dataOffset, dataLength));
            }
        }

        #endregion

        #region Keep Alive

        private void StartKeepAliveTimeoutTimer()
        {
            _keepAliveTimeoutTimer.Change((int)KeepAliveTimeout.TotalMilliseconds, Timeout.Infinite);
        }

        private void StopKeepAliveTimeoutTimer()
        {
            _keepAliveTimeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
        }

        private void OnKeepAliveTimeout()
        {
            _log(string.Format("Keep-alive timer timeout [{1}].", KeepAliveTimeout));
            Close(WebSocketCloseCode.AbnormalClosure, "Keep-Alive Timeout");
        }

        private void OnKeepAlive()
        {
            if (_keepAliveLocker.WaitOne(0))
            {
                try
                {
                    if (State != WebSocketState.Open)
                        return;

                    if (_keepAliveTracker.ShouldSendKeepAlive())
                    {
                        var keepAliveFrame = new PingFrame().ToArray(_frameBuilder);
                        //SendFrame(keepAliveFrame);
                        StartKeepAliveTimeoutTimer();

                        _keepAliveTracker.ResetTimer();
                    }
                }
                catch (Exception ex)
                {
                    _log(ex.Message);
                    Close(WebSocketCloseCode.EndpointUnavailable);
                }
                finally
                {
                    _keepAliveLocker.Release();
                }
            }
        }

        #endregion

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (disposing)
            {
                try
                {
                    Close();
                }
                catch (Exception ex)
                {
                    _log(ex.Message);
                }
            }
        }

        #endregion
    }
}
