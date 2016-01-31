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

        private static readonly ILog _log = Logger.Get<TcpSocketServer>();
        private IBufferManager _bufferManager;
        //private TcpListener _listener;
        //private readonly ConcurrentDictionary<string, TcpSocketSession> _sessions = new ConcurrentDictionary<string, TcpSocketSession>();
        private readonly object _opsLock = new object();
        private readonly TcpSocketSaeaServerConfiguration _configuration;

        #endregion

        private Socket _listener;
        private SaeaPool _pool;

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

            _pool = new SaeaPool();
            //for (int i = 0; i < this.socketListenerSettings.MaxAcceptOps; i++)
            //{
            //    // add SocketAsyncEventArg to the pool
            //    this.poolOfAcceptEventArgs.Push(CreateNewSaeaForAccept(poolOfAcceptEventArgs));
            //}

            _listener = new Socket(this.ListenedEndPoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);
            _listener.Bind(this.ListenedEndPoint);
            _listener.Listen(_configuration.PendingConnectionBacklog);

            ConfigureListener();
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

        #endregion

        #region Properties

        public IPEndPoint ListenedEndPoint { get; private set; }
        public bool Active { get { return _state == _listening; } }
        //public int SessionCount { get { return _sessions.Count; } }

        #endregion
    }
}
