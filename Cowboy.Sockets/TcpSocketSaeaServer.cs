using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
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
        //private TcpListener _listener;
        //private readonly ConcurrentDictionary<string, TcpSocketSession> _sessions = new ConcurrentDictionary<string, TcpSocketSession>();
        private readonly object _opsLock = new object();
        private readonly TcpSocketSaeaServerConfiguration _configuration;

        #endregion

        private Socket _listener;
        private SaeaPool _sessionAcceptSaeaPool;
        private SaeaPool _sessionHandleSaeaPool;

        private int _state;
        private const int _none = 0;
        private const int _listening = 1;
        private const int _disposed = 5;

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
            _listener = new Socket(this.ListenedEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(this.ListenedEndPoint);

            ConfigureListener();

            _listener.Listen(_configuration.PendingConnectionBacklog);

            StartAccept();
        }

        public void Stop()
        {

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
            if (saea.SocketError != SocketError.Success)
            {
                StartAccept();

                saea.AcceptSocket.Close();
                saea.AcceptSocket = null;
                _sessionAcceptSaeaPool.Push(saea);

                return;
            }

            SocketAsyncEventArgs sessionHandleSaea = null;
            if (!_sessionHandleSaeaPool.TryPop(out sessionHandleSaea))
            {
                throw new Exception();
            }

            sessionHandleSaea.AcceptSocket = saea.AcceptSocket;

            saea.AcceptSocket = null;
            _sessionAcceptSaeaPool.Push(saea);

            StartReceive(sessionHandleSaea);

            StartAccept();
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
                    throw new ArgumentException("The last operation completed on the socket was not a receive or send");
            }
        }

        private void StartReceive(SocketAsyncEventArgs saea)
        {
            //saea.SetBuffer(receiveSendToken.bufferOffsetReceive, this.socketListenerSettings.BufferSize);

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
                CloseClientSocket(saea);
                return;
            }

            if (saea.BytesTransferred == 0)
            {
                CloseClientSocket(saea);
                return;
            }

            //int remainingBytesToProcess = saea.BytesTransferred;

            //if (receiveSendToken.receivedPrefixBytesDoneCount < this.socketListenerSettings.ReceivePrefixLength)
            //{
            //    remainingBytesToProcess = prefixHandler.HandlePrefix(saea, receiveSendToken, remainingBytesToProcess);

            //    if (remainingBytesToProcess == 0)
            //    {
            //        StartReceive(saea);
            //        return;
            //    }
            //}

            //bool incomingTcpMessageIsReady = messageHandler.HandleMessage(saea, receiveSendToken, remainingBytesToProcess);

            //if (incomingTcpMessageIsReady == true)
            //{
            //    receiveSendToken.theMediator.HandleData(receiveSendToken.theDataHolder);
            //    receiveSendToken.CreateNewDataHolder();
            //    receiveSendToken.Reset();

            //    receiveSendToken.theMediator.PrepareOutgoingData();
            //    StartSend(receiveSendToken.theMediator.GiveBack());
            //}
            //else
            //{
            //    receiveSendToken.receiveMessageOffset = receiveSendToken.bufferOffsetReceive;

            //    receiveSendToken.recPrefixBytesDoneThisOp = 0;

            //    StartReceive(saea);
            //}
        }

        private void CloseClientSocket(SocketAsyncEventArgs saea)
        {
            //var receiveSendToken = (saea.UserToken as DataHoldingUserToken);

            //try
            //{
            //    saea.AcceptSocket.Shutdown(SocketShutdown.Both);
            //}
            //catch (Exception)
            //{
            //}

            //saea.AcceptSocket.Close();

            //if (receiveSendToken.theDataHolder.dataMessageReceived != null)
            //{
            //    receiveSendToken.CreateNewDataHolder();
            //}

            //this.poolOfRecSendEventArgs.Push(saea);

            //Interlocked.Decrement(ref this.numberOfAcceptedSockets);

            //this.theMaxConnectionsEnforcer.Release();
        }

        private void StartSend(SocketAsyncEventArgs receiveSendEventArgs)
        {
            //DataHoldingUserToken receiveSendToken = (DataHoldingUserToken)receiveSendEventArgs.UserToken;

            //if (receiveSendToken.sendBytesRemainingCount <= this.socketListenerSettings.BufferSize)
            //{
            //    receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetSend, receiveSendToken.sendBytesRemainingCount);
            //    Buffer.BlockCopy(receiveSendToken.dataToSend, receiveSendToken.bytesSentAlreadyCount, receiveSendEventArgs.Buffer, receiveSendToken.bufferOffsetSend, receiveSendToken.sendBytesRemainingCount);
            //}
            //else
            //{
            //    receiveSendEventArgs.SetBuffer(receiveSendToken.bufferOffsetSend, this.socketListenerSettings.BufferSize);
            //    Buffer.BlockCopy(receiveSendToken.dataToSend, receiveSendToken.bytesSentAlreadyCount, receiveSendEventArgs.Buffer, receiveSendToken.bufferOffsetSend, this.socketListenerSettings.BufferSize);
            //}

            //bool willRaiseEvent = receiveSendEventArgs.AcceptSocket.SendAsync(receiveSendEventArgs);

            //if (!willRaiseEvent)
            //{
            //    ProcessSend(receiveSendEventArgs);
            //}
        }

        private void ProcessSend(SocketAsyncEventArgs receiveSendEventArgs)
        {
            //DataHoldingUserToken receiveSendToken = (DataHoldingUserToken)receiveSendEventArgs.UserToken;

            //if (receiveSendEventArgs.SocketError == SocketError.Success)
            //{
            //    receiveSendToken.sendBytesRemainingCount = receiveSendToken.sendBytesRemainingCount - receiveSendEventArgs.BytesTransferred;

            //    if (receiveSendToken.sendBytesRemainingCount == 0)
            //    {
            //        StartReceive(receiveSendEventArgs);
            //    }
            //    else
            //    {                   
            //        receiveSendToken.bytesSentAlreadyCount += receiveSendEventArgs.BytesTransferred;
            //        StartSend(receiveSendEventArgs);
            //    }
            //}
            //else
            //{
            //    receiveSendToken.Reset();
            //    CloseClientSocket(receiveSendEventArgs);
            //}
        }
    }
}
