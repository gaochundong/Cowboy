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

        private Socket _listener;
        private SaeaPool _sessionAcceptSaeaPool;
        private SaeaPool _sessionHandleSaeaPool;

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;

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

            _sessionAcceptSaeaPool = new SaeaPool();
            _sessionHandleSaeaPool = new SaeaPool();
            for (int i = 0; i < 10; i++)
            {
                _sessionAcceptSaeaPool.Push(CreateSaeaForSessionAccept());
            }
            for (int i = 0; i < 100; i++)
            {
                _sessionHandleSaeaPool.Push(CreateSaeaForSessionHandle());
            }
        }

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool Active { get { return _state == _listening; } }
        //public int SessionCount { get { return _sessions.Count; } }

        #endregion

        public void Start()
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

                StartAccept();
            }
            catch (Exception ex) when (!ShouldThrow(ex)) { }
        }

        public void Stop()
        {
            if (Interlocked.Exchange(ref _state, _disposed) == _disposed)
            {
                return;
            }

            try
            {
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

        private SocketAsyncEventArgs CreateSaeaForSessionAccept()
        {
            var saea = new SocketAsyncEventArgs();

            saea.Completed += OnSessionAcceptSaeaCompleted;

            return saea;
        }

        private void OnSessionAcceptSaeaCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        private void StartAccept()
        {
            SocketAsyncEventArgs sessionAcceptSaea = null;
            if (!_sessionAcceptSaeaPool.TryPop(out sessionAcceptSaea))
            {
                _log.ErrorFormat("xxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                throw new Exception();
            }

            bool isIoOperationPending = _listener.AcceptAsync(sessionAcceptSaea);
            if (!isIoOperationPending)
            {
                ProcessAccept(sessionAcceptSaea);
            }
        }

        private void ProcessAccept(SocketAsyncEventArgs saea)
        {
            StartAccept();

            if (saea.SocketError != SocketError.Success)
            {
                _log.ErrorFormat("xxxxxxxxxxxxxxxxxxxxxxxxxxxxx");

                saea.AcceptSocket.Close();
                saea.AcceptSocket = null;
                _sessionAcceptSaeaPool.Push(saea);
                return;
            }

            SocketAsyncEventArgs sessionHandleSaea = null;
            if (!_sessionHandleSaeaPool.TryPop(out sessionHandleSaea))
            {
                _log.ErrorFormat("xxxxxxxxxxxxxxxxxxxxxxxxxxxxx");
                throw new Exception();
            }

            sessionHandleSaea.AcceptSocket = saea.AcceptSocket;
            saea.AcceptSocket = null;
            _sessionAcceptSaeaPool.Push(saea);

            StartReceive(sessionHandleSaea);
        }

        private SocketAsyncEventArgs CreateSaeaForSessionHandle()
        {
            var saea = new SocketAsyncEventArgs();

            saea.Completed += OnSessionHandleSaeaCompleted;

            return saea;
        }

        private void OnSessionHandleSaeaCompleted(object sender, SocketAsyncEventArgs e)
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
                        string.Format("The last operation [{0}] completed on the socket was not a receive or send.", e.LastOperation));
            }
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
