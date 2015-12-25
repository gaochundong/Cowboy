using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public class AsyncTcpSocketClient
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<AsyncTcpSocketClient>();
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private readonly IAsyncTcpSocketClientMessageDispatcher _dispatcher;
        private readonly TcpSocketClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private byte[] _receiveBuffer;
        private byte[] _sessionBuffer;
        private int _sessionBufferCount = 0;

        #endregion

        #region Constructors

        public AsyncTcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPAddress localIPAddress, int localPort, IAsyncTcpSocketClientMessageDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), new IPEndPoint(localIPAddress, localPort), dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPEndPoint localEP, IAsyncTcpSocketClientMessageDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), localEP, dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPAddress remoteIPAddress, int remotePort, IAsyncTcpSocketClientMessageDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPEndPoint remoteEP, IAsyncTcpSocketClientMessageDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
            : this(remoteEP, null, dispatcher, configuration)
        {
        }

        public AsyncTcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP, IAsyncTcpSocketClientMessageDispatcher dispatcher, TcpSocketClientConfiguration configuration = null)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");
            if (dispatcher == null)
                throw new ArgumentNullException("dispatcher");

            _remoteEndPoint = remoteEP;
            _localEndPoint = localEP;
            _dispatcher = dispatcher;
            _configuration = configuration ?? new TcpSocketClientConfiguration();

            Initialize();
        }

        private void Initialize()
        {
            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);
        }

        #endregion

        #region Properties

        public bool Connected { get { return _tcpClient != null && _tcpClient.Client.Connected; } }
        public EndPoint RemoteEndPoint { get { return _tcpClient.Client.RemoteEndPoint; } }
        public EndPoint LocalEndPoint { get { return _tcpClient.Client.LocalEndPoint; } }

        #endregion

        #region Connect

        public void Connect()
        {
            if (Connected)
                return;

            Task.Run(async () =>
            {
                await Process();
            });
        }

        private async Task Process()
        {
            if (_localEndPoint != null)
            {
                _tcpClient = new TcpClient(_localEndPoint);
            }
            else
            {
                _tcpClient = new TcpClient();
            }

            await _tcpClient.ConnectAsync(_remoteEndPoint.Address, _remoteEndPoint.Port);

            _log.DebugFormat("Connected to server [{0}] with dispatcher [{1}] on [{2}].",
                this.RemoteEndPoint, _dispatcher.GetType().Name, DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));

            _receiveBuffer = _bufferManager.BorrowBuffer();
            _sessionBuffer = _bufferManager.BorrowBuffer();
            _sessionBufferCount = 0;

            try
            {
                while (Connected)
                {
                    int receiveCount = await _tcpClient.GetStream().ReadAsync(_receiveBuffer, 0, _receiveBuffer.Length);
                    if (receiveCount == 0)
                        break;

                    if (!_configuration.IsPackingEnabled)
                    {
                        await _dispatcher.Dispatch(this, _receiveBuffer, 0, receiveCount);
                    }
                    else
                    {
                        AppendBuffer(_receiveBuffer, receiveCount, ref _sessionBuffer, ref _sessionBufferCount);

                        while (true)
                        {
                            var packetHeader = TcpPacketHeader.ReadHeader(_sessionBuffer);
                            if (TcpPacketHeader.HEADER_SIZE + packetHeader.PayloadSize <= _sessionBufferCount)
                            {
                                await _dispatcher.Dispatch(this, _sessionBuffer, TcpPacketHeader.HEADER_SIZE, packetHeader.PayloadSize);
                                ShiftBuffer(TcpPacketHeader.HEADER_SIZE + packetHeader.PayloadSize, ref _sessionBuffer, ref _sessionBufferCount);
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
            catch (SocketException) { }
            finally
            {
                _bufferManager.ReturnBuffer(_receiveBuffer);
                _bufferManager.ReturnBuffer(_sessionBuffer);

                _log.DebugFormat("Disconnected from server [{0}] with dispatcher [{1}] on [{2}].",
                    this.RemoteEndPoint, _dispatcher.GetType().Name, DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"));

                if (_tcpClient != null)
                    _tcpClient.Dispose();
            }
        }

        public void Close()
        {
            if (_tcpClient != null && _tcpClient.Connected)
            {
                _tcpClient.Close();
            }
        }

        private void AppendBuffer(byte[] receiveBuffer, int receiveCount, ref byte[] sessionBuffer, ref int sessionBufferCount)
        {
            if (sessionBuffer.Length < (sessionBufferCount + receiveCount))
            {
                byte[] autoExpandedBuffer = _bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Length < (sessionBufferCount + receiveCount) * 2)
                {
                    _bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new byte[(sessionBufferCount + receiveCount) * 2];
                }

                Array.Copy(sessionBuffer, 0, autoExpandedBuffer, 0, sessionBufferCount);

                var discardBuffer = sessionBuffer;
                sessionBuffer = autoExpandedBuffer;
                _bufferManager.ReturnBuffer(discardBuffer);
            }

            Array.Copy(receiveBuffer, 0, sessionBuffer, sessionBufferCount, receiveCount);
            sessionBufferCount = sessionBufferCount + receiveCount;
        }

        private void ShiftBuffer(int shiftStart, ref byte[] sessionBuffer, ref int sessionBufferCount)
        {
            if ((sessionBufferCount - shiftStart) < shiftStart)
            {
                Array.Copy(sessionBuffer, shiftStart, sessionBuffer, 0, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;
            }
            else
            {
                byte[] copyBuffer = _bufferManager.BorrowBuffer();
                if (copyBuffer.Length < (sessionBufferCount - shiftStart))
                {
                    _bufferManager.ReturnBuffer(copyBuffer);
                    copyBuffer = new byte[sessionBufferCount - shiftStart];
                }

                Array.Copy(sessionBuffer, shiftStart, copyBuffer, 0, sessionBufferCount - shiftStart);
                Array.Copy(copyBuffer, 0, sessionBuffer, 0, sessionBufferCount - shiftStart);
                sessionBufferCount = sessionBufferCount - shiftStart;

                _bufferManager.ReturnBuffer(copyBuffer);
            }
        }

        #endregion

        #region Send

        public async Task Send(byte[] data)
        {
            await Send(data, 0, data.Length);
        }

        public async Task Send(byte[] data, int offset, int count)
        {
            if (!_configuration.IsPackingEnabled)
            {
                await _tcpClient.GetStream().WriteAsync(data, offset, count);
            }
            else
            {
                var packet = TcpPacket.FromPayload(data, offset, count);
                var packetArray = packet.ToArray();
                await _tcpClient.GetStream().WriteAsync(packetArray, 0, packetArray.Length);
            }
        }

        #endregion
    }
}
