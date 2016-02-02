using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public class TcpSocketSaeaServer
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketSaeaServer>();
        private IBufferManager _bufferManager;
        private readonly ConcurrentDictionary<string, TcpSocketSaeaSession> _sessions = new ConcurrentDictionary<string, TcpSocketSaeaSession>();
        private readonly TcpSocketSaeaServerConfiguration _configuration;

        private readonly object _opsLock = new object();
        private bool _isListening = false;
        private Socket _listener;
        private SaeaPool _acceptSaeaPool;
        private SaeaPool _handleSaeaPool;
        private SessionPool _sessionPool;

        #endregion

        #region Constructors

        public TcpSocketSaeaServer(int listenedPort, TcpSocketSaeaServerConfiguration configuration = null)
            : this(IPAddress.Any, listenedPort, configuration)
        {
        }

        public TcpSocketSaeaServer(IPAddress listenedAddress, int listenedPort, TcpSocketSaeaServerConfiguration configuration = null)
            : this(new IPEndPoint(listenedAddress, listenedPort), configuration)
        {
        }

        public TcpSocketSaeaServer(IPEndPoint listenedEndPoint, TcpSocketSaeaServerConfiguration configuration = null)
        {
            if (listenedEndPoint == null)
                throw new ArgumentNullException("listenedEndPoint");

            this.ListenedEndPoint = listenedEndPoint;
            _configuration = configuration ?? new TcpSocketSaeaServerConfiguration();

            if (_configuration.FrameBuilder == null)
                throw new InvalidProgramException("The frame handler in configuration cannot be null.");

            Initialize();
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);

            _acceptSaeaPool = new SaeaPool(16, 32,
                () =>
                {
                    var saea = new SocketAsyncEventArgs();
                    saea.Completed += OnAcceptSaeaCompleted;
                    return saea;
                },
                (saea) =>
                {
                    var socket = saea.AcceptSocket;
                    if (socket != null)
                    {
                        saea.AcceptSocket = null;

                        try
                        {
                            socket.Close(0);
                        }
                        catch (Exception) { }
                    }
                });
            _handleSaeaPool = new SaeaPool(1024, int.MaxValue,
                () =>
                {
                    var saea = new SocketAsyncEventArgs();
                    saea.Completed += OnHandleSaeaCompleted;
                    return saea;
                },
                (saea) =>
                {
                    var socket = saea.AcceptSocket;
                    if (socket != null)
                    {
                        saea.AcceptSocket = null;

                        try
                        {
                            socket.Close(0);
                        }
                        catch (Exception) { }
                    }
                });
            _sessionPool = new SessionPool(1024, int.MaxValue,
                () =>
                {
                    var session = new TcpSocketSaeaSession(_configuration, _bufferManager, _handleSaeaPool, this);
                    return session;
                },
                (session) =>
                {
                    //var socket = session.AcceptSocket;
                    //if (socket != null)
                    //{
                    //    session.AcceptSocket = null;

                    //    try
                    //    {
                    //        socket.Close(0);
                    //    }
                    //    catch (Exception) { }
                    //}
                });
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool IsListening { get { return _isListening; } }
        //public int SessionCount { get { return _sessions.Count; } }

        #endregion

        public void Listen()
        {
            lock (_opsLock)
            {
                if (_isListening)
                    return;

                try
                {
                    _listener = new Socket(this.ListenedEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
                    _listener.Bind(this.ListenedEndPoint);

                    ConfigureListener();

                    _listener.Listen(_configuration.PendingConnectionBacklog);
                    _isListening = true;

                    var addr = _listener.LocalEndPoint.Serialize();


                    StartAccept();
                }
                catch (Exception ex) when (!ShouldThrow(ex)) { }
            }
        }

        public void Shutdown()
        {
            lock (_opsLock)
            {
                if (!_isListening)
                    return;

                try
                {
                    _isListening = false;
                    _listener.Dispose();

                    //foreach (var session in _sessions.Values)
                    //{
                    //    await session.Close();
                    //}
                }
                catch (Exception ex) when (!ShouldThrow(ex)) { }
                finally
                {
                    _listener = null;
                }
            }
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

        private void OnAcceptSaeaCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void OnHandleSaeaCompleted(object sender, SocketAsyncEventArgs e)
        {
            switch (e.LastOperation)
            {
                case SocketAsyncOperation.Receive:
                    ProcessReceive(e);
                    break;
                case SocketAsyncOperation.Send:
                    ProcessSend(e);
                    break;
                default:
                    throw new InvalidOperationException(
                        string.Format("The last operation [{0}] completed on the socket was not a receive or send.",
                        e.LastOperation));
            }
        }

        private void StartAccept()
        {
            var saea = _acceptSaeaPool.Take();

            bool isIoOperationPending = _listener.AcceptAsync(saea);
            if (!isIoOperationPending)
            {
                ProcessAccept(saea);
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs saea)
        {
            StartAccept();

            if (saea.SocketError != SocketError.Success)
            {
                _log.ErrorFormat("Error occurred when accept an incoming socket [{0}].", saea.SocketError);
                _acceptSaeaPool.Return(saea);
                return;
            }

            var session = _sessionPool.Take();
            session.Assign(saea.AcceptSocket);
            session.Start();

            saea.AcceptSocket = null;
            _acceptSaeaPool.Return(saea);



            //StartReceive(sessionHandleSaea);
        }

        private void StartReceive(SocketAsyncEventArgs saea)
        {
            var buffer = _bufferManager.BorrowBuffer();
            saea.SetBuffer(buffer, 0, buffer.Length);

            bool isIoOperationPending = saea.AcceptSocket.ReceiveAsync(saea);
            if (!isIoOperationPending)
            {
                ProcessReceive(saea);
            }
        }

        private void ProcessReceive(SocketAsyncEventArgs saea)
        {
            if (saea.SocketError != SocketError.Success)
            {
                _log.ErrorFormat("xxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

                CloseSession(saea);
                return;
            }

            var receiveCount = saea.BytesTransferred;
            if (receiveCount == 0)
            {
                CloseSession(saea);
                return;
            }

            var buffer = new byte[99999];
            Array.Copy(saea.Buffer, 0, buffer, 0, receiveCount);

            StartReceive(saea);
        }

        private void StartSend(SocketAsyncEventArgs saea)
        {
            bool isIoOperationPending = saea.AcceptSocket.SendAsync(saea);
            if (!isIoOperationPending)
            {
                ProcessSend(saea);
            }
        }

        private void ProcessSend(SocketAsyncEventArgs saea)
        {
            if (saea.SocketError == SocketError.Success)
            {
                //receiveSendToken.sendBytesRemainingCount = receiveSendToken.sendBytesRemainingCount - saea.BytesTransferred;

                //if (receiveSendToken.sendBytesRemainingCount == 0)
                //{
                //    StartReceive(saea);
                //}
                //else
                //{
                //    receiveSendToken.bytesSentAlreadyCount += saea.BytesTransferred;
                //    StartSend(saea);
                //}
            }
            else
            {
                CloseSession(saea);
            }
        }

        private void CloseSession(SocketAsyncEventArgs saea)
        {
            try
            {
                saea.AcceptSocket.Shutdown(SocketShutdown.Both);
            }
            catch { }

            try
            {
                saea.AcceptSocket.Dispose();
            }
            catch { }
        }
    }
}
