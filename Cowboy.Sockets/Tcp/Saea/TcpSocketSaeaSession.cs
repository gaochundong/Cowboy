using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketSaeaSession
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketSaeaSession>();
        private readonly object _opsLock = new object();
        private readonly TcpSocketSaeaServerConfiguration _configuration;
        private readonly IBufferManager _bufferManager;
        private readonly SaeaPool _saeaPool;
        private readonly TcpSocketSaeaServer _server;
        private Socket _socket;
        private string _sessionKey;
        private IPEndPoint _remoteEndPoint;
        private IPEndPoint _localEndPoint;
        private bool _closed = false;

        #endregion

        #region Constructors

        public TcpSocketSaeaSession(
            TcpSocketSaeaServerConfiguration configuration,
            IBufferManager bufferManager,
            SaeaPool saeaPool,
            TcpSocketSaeaServer server)
        {
            if (configuration == null)
                throw new ArgumentNullException("configuration");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");
            if (saeaPool == null)
                throw new ArgumentNullException("saeaPool");
            if (server == null)
                throw new ArgumentNullException("server");

            _configuration = configuration;
            _bufferManager = bufferManager;
            _saeaPool = saeaPool;
            _server = server;
        }

        #endregion

        #region Properties

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get; private set; }
        public bool Connected { get { return _socket != null && _socket.Connected; } }
        public IPEndPoint RemoteEndPoint { get { return Connected ? (IPEndPoint)_socket.RemoteEndPoint : _remoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return Connected ? (IPEndPoint)_socket.LocalEndPoint : _localEndPoint; } }
        public TcpSocketSaeaServer Server { get { return _server; } }
        public TimeSpan ConnectTimeout { get { return _configuration.ConnectTimeout; } }

        public override string ToString()
        {
            return string.Format("SessionKey[{0}], RemoteEndPoint[{1}], LocalEndPoint[{2}]",
                this.SessionKey, this.RemoteEndPoint, this.LocalEndPoint);
        }

        #endregion

        internal void Assign(Socket socket)
        {
            if (socket == null)
                throw new ArgumentNullException("socket");

            lock (_opsLock)
            {
                _socket = socket;
                _sessionKey = Guid.NewGuid().ToString();
                this.StartTime = DateTime.UtcNow;

                _remoteEndPoint = Connected ? (IPEndPoint)_socket.RemoteEndPoint : null;
                _localEndPoint = Connected ? (IPEndPoint)_socket.LocalEndPoint : null;
            }
        }

        internal void Start()
        {
            lock (_opsLock)
            {
                try
                {
                    if (Connected)
                    {
                        _closed = false;

                        var saea = _saeaPool.Take();
                        var receiveBuffer = _bufferManager.BorrowBuffer();
                        var sessionBuffer = _bufferManager.BorrowBuffer();
                        int sessionBufferCount = 0;

                        bool isErrorOccurredInUserSide = false;
                        try
                        {
                            //_server.RaiseClientConnected(this);
                        }
                        catch (Exception ex)
                        {
                            isErrorOccurredInUserSide = true;
                            HandleUserSideError(ex);
                        }

                        if (!isErrorOccurredInUserSide)
                        {
                            //ContinueReadBuffer();
                        }
                        else
                        {
                            Close();
                        }
                    }
                }
                catch (Exception ex)
                {
                    _log.Error(ex.Message, ex);
                    Close();
                }
            }
        }

        public void Close()
        {
            lock (_opsLock)
            {
                if (!_closed)
                {
                    _closed = true;

                    try
                    {
                        //if (_stream != null)
                        //{
                        //    _stream.Close();
                        //    _stream = null;
                        //}
                        //if (_tcpClient != null && _tcpClient.Connected)
                        //{
                        //    _tcpClient.Close();
                        //    _tcpClient = null;
                        //}
                    }
                    catch (Exception ex)
                    {
                        _log.Error(string.Format("Session [{0}] exception occurred, [{1}].", this, ex.Message), ex);
                    }
                    finally
                    {
                        //_bufferManager.ReturnBuffer(_receiveBuffer);
                        //_bufferManager.ReturnBuffer(_sessionBuffer);
                    }

                    try
                    {
                        //_server.RaiseClientDisconnected(this);
                    }
                    catch (Exception ex)
                    {
                        HandleUserSideError(ex);
                    }
                }
            }
        }

        private bool CloseIfShould(Exception ex)
        {
            if (ex is SocketException
                || ex is IOException
                || ex is InvalidOperationException
                || ex is ObjectDisposedException
                || ex is NullReferenceException
                )
            {
                if (ex is SocketException)
                    _log.Error(string.Format("Session [{0}] exception occurred, [{1}].", this, ex.Message), ex);

                // connection has been closed
                Close();

                return true;
            }

            return false;
        }

        private static void HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Error occurred in user side [{0}].", ex.Message), ex);
        }
    }
}
