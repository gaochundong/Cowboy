using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public class TcpSocketClient
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketClient>();
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private readonly object _opsLock = new object();
        private readonly TcpSocketClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;
        private byte[] _receiveBuffer;
        private byte[] _sessionBuffer;
        private int _sessionBufferCount = 0;

        #endregion

        #region Constructors

        public TcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPAddress localIPAddress, int localPort, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), new IPEndPoint(localIPAddress, localPort), configuration)
        {
        }

        public TcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPEndPoint localEP = null, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), localEP, configuration)
        {
        }

        public TcpSocketClient(IPEndPoint remoteEP, IPEndPoint localEP = null, TcpSocketClientConfiguration configuration = null)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");

            _remoteEndPoint = remoteEP;
            _localEndPoint = localEP;
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
            lock (_opsLock)
            {
                if (!Connected)
                {
                    if (_localEndPoint != null)
                    {
                        _tcpClient = new TcpClient(_localEndPoint);
                    }
                    else
                    {
                        _tcpClient = new TcpClient();
                    }
                    ConfigureClient();

                    _receiveBuffer = _bufferManager.BorrowBuffer();
                    _sessionBuffer = _bufferManager.BorrowBuffer();
                    _sessionBufferCount = 0;

                    _tcpClient.BeginConnect(_remoteEndPoint.Address, _remoteEndPoint.Port, HandleTcpServerConnected, _tcpClient);
                }
            }
        }

        private void ConfigureClient()
        {
            _tcpClient.ReceiveBufferSize = _configuration.ReceiveBufferSize;
            _tcpClient.SendBufferSize = _configuration.SendBufferSize;
            _tcpClient.ReceiveTimeout = (int)_configuration.ReceiveTimeout.TotalMilliseconds;
            _tcpClient.SendTimeout = (int)_configuration.SendTimeout.TotalMilliseconds;
            _tcpClient.ExclusiveAddressUse = _configuration.ExclusiveAddressUse;
            _tcpClient.NoDelay = _configuration.NoDelay;
            _tcpClient.LingerState = _configuration.LingerState;
        }

        public void Close()
        {
            lock (_opsLock)
            {
                if (Connected)
                {
                    try
                    {
                        _tcpClient.Close();

                        RaiseServerDisconnected();
                    }
                    finally
                    {
                        _bufferManager.ReturnBuffer(_receiveBuffer);
                        _bufferManager.ReturnBuffer(_sessionBuffer);
                    }
                }
            }
        }

        #endregion

        #region Receive

        private void HandleTcpServerConnected(IAsyncResult ar)
        {
            try
            {
                _tcpClient.EndConnect(ar);
                RaiseServerConnected();

                // we are connected successfully and start async read operation.
                ContinueReadBuffer();
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
            }
        }

        private void ContinueReadBuffer()
        {
            try
            {
                // buffer : An array of type Byte that is the location in memory to store data read from the NetworkStream.
                // offset : The location in buffer to begin storing the data.
                // size : The number of bytes to read from the NetworkStream.
                // callback : The AsyncCallback delegate that is executed when BeginRead completes.
                // state : An object that contains any additional user-defined data.
                _tcpClient.GetStream().BeginRead(_receiveBuffer, 0, _receiveBuffer.Length, HandleDataReceived, _tcpClient);
            }
            catch (Exception ex)
            {
                if (!ShouldClose(ex))
                    throw;
            }
        }

        private void HandleDataReceived(IAsyncResult ar)
        {
            try
            {
                int numberOfReadBytes = 0;
                try
                {
                    // The EndRead method blocks until data is available. The EndRead method reads 
                    // as much data as is available up to the number of bytes specified in the size 
                    // parameter of the BeginRead method. If the remote host shuts down the Socket 
                    // connection and all available data has been received, the EndRead method 
                    // completes immediately and returns zero bytes.
                    numberOfReadBytes = _tcpClient.GetStream().EndRead(ar);
                }
                catch (Exception ex)
                {
                    // unable to read data from transport connection, 
                    // the existing connection was forcibly closes by remote host
                    numberOfReadBytes = 0;

                    if (!(ex is IOException))
                        _log.Error(ex.Message, ex);
                }

                if (numberOfReadBytes == 0)
                {
                    // connection has been closed
                    Close();
                    return;
                }

                // received bytes and trigger notifications
                ReceiveBuffer(numberOfReadBytes);

                // then start reading from the network again
                ContinueReadBuffer();
            }
            catch (Exception ex)
            {
                if (!ShouldClose(ex))
                    throw;
            }
        }

        private void ReceiveBuffer(int receivedBufferLength)
        {
            if (!_configuration.IsFramingEnabled)
            {
                // yeah, we received the buffer and then raise it to user side to handle.
                RaiseServerDataReceived(_receiveBuffer, 0, receivedBufferLength);
            }
            else
            {
                // TCP guarantees delivery of all packets in the correct order. 
                // But there is no guarantee that one write operation on the sender-side will result in 
                // one read event on the receiving side. One call of write(message) by the sender 
                // can result in multiple messageReceived(session, message) events on the receiver; 
                // and multiple calls of write(message) can lead to a single messageReceived event.
                // In a stream-based transport such as TCP/IP, received data is stored into a socket receive buffer. 
                // Unfortunately, the buffer of a stream-based transport is not a queue of packets but a queue of bytes. 
                // It means, even if you sent two messages as two independent packets, 
                // an operating system will not treat them as two messages but as just a bunch of bytes. 
                // Therefore, there is no guarantee that what you read is exactly what your remote peer wrote.
                // There are three common techniques for splitting the stream of bytes into messages:
                //   1. use fixed length messages
                //   2. use a fixed length header that indicates the length of the body
                //   3. using a delimiter; for example many text-based protocols append
                //      a newline (or CR LF pair) after every message.
                AppendBuffer(receivedBufferLength);
                while (true)
                {
                    var frameHeader = TcpFrameHeader.ReadHeader(_sessionBuffer);
                    if (TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize <= _sessionBufferCount)
                    {
                        // yeah, we received the buffer and then raise it to user side to handle.
                        RaiseServerDataReceived(_sessionBuffer, TcpFrameHeader.HEADER_SIZE, frameHeader.PayloadSize);

                        // remove the received packet from buffer
                        ShiftBuffer(TcpFrameHeader.HEADER_SIZE + frameHeader.PayloadSize);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void AppendBuffer(int appendedCount)
        {
            if (appendedCount <= 0) return;

            if (_sessionBuffer.Length < (_sessionBufferCount + appendedCount))
            {
                byte[] autoExpandedBuffer = _bufferManager.BorrowBuffer();
                if (autoExpandedBuffer.Length < (_sessionBufferCount + appendedCount) * 2)
                {
                    _bufferManager.ReturnBuffer(autoExpandedBuffer);
                    autoExpandedBuffer = new byte[(_sessionBufferCount + appendedCount) * 2];
                }

                Array.Copy(_sessionBuffer, 0, autoExpandedBuffer, 0, _sessionBufferCount);

                var discardBuffer = _sessionBuffer;
                _sessionBuffer = autoExpandedBuffer;
                _bufferManager.ReturnBuffer(discardBuffer);
            }

            Array.Copy(_receiveBuffer, 0, _sessionBuffer, _sessionBufferCount, appendedCount);
            _sessionBufferCount = _sessionBufferCount + appendedCount;
        }

        private void ShiftBuffer(int shiftStart)
        {
            if ((_sessionBufferCount - shiftStart) < shiftStart)
            {
                Array.Copy(_sessionBuffer, shiftStart, _sessionBuffer, 0, _sessionBufferCount - shiftStart);
                _sessionBufferCount = _sessionBufferCount - shiftStart;
            }
            else
            {
                byte[] copyBuffer = _bufferManager.BorrowBuffer();
                if (copyBuffer.Length < (_sessionBufferCount - shiftStart))
                {
                    _bufferManager.ReturnBuffer(copyBuffer);
                    copyBuffer = new byte[_sessionBufferCount - shiftStart];
                }

                Array.Copy(_sessionBuffer, shiftStart, copyBuffer, 0, _sessionBufferCount - shiftStart);
                Array.Copy(copyBuffer, 0, _sessionBuffer, 0, _sessionBufferCount - shiftStart);
                _sessionBufferCount = _sessionBufferCount - shiftStart;

                _bufferManager.ReturnBuffer(copyBuffer);
            }
        }

        #endregion

        #region Send

        public void Send(byte[] data)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            Send(data, 0, data.Length);
        }

        public void Send(byte[] data, int offset, int count)
        {
            if (data == null)
                throw new ArgumentNullException("data");

            if (!Connected)
            {
                RaiseServerDisconnected();
                throw new InvalidProgramException("This client has not connected to server.");
            }

            try
            {
                if (!_configuration.IsFramingEnabled)
                {
                    _tcpClient.GetStream().BeginWrite(data, offset, count, HandleDataWritten, _tcpClient);
                }
                else
                {
                    var frame = TcpFrame.FromPayload(data, offset, count);
                    var frameBuffer = frame.ToArray();
                    _tcpClient.GetStream().BeginWrite(frameBuffer, 0, frameBuffer.Length, HandleDataWritten, _tcpClient);
                }
            }
            catch (Exception ex)
            {
                if (!ShouldClose(ex))
                    throw;
            }
        }

        private void HandleDataWritten(IAsyncResult ar)
        {
            try
            {
                _tcpClient.GetStream().EndWrite(ar);
            }
            catch (Exception ex)
            {
                if (!ShouldClose(ex))
                    throw;
            }
        }

        private bool ShouldClose(Exception ex)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is IOException)
            {
                _log.Error(ex.Message, ex);

                // connection has been closed
                Close();

                return true;
            }

            return false;
        }

        #endregion

        #region Events

        public event EventHandler<TcpServerConnectedEventArgs> ServerConnected;
        public event EventHandler<TcpServerDisconnectedEventArgs> ServerDisconnected;
        public event EventHandler<TcpServerDataReceivedEventArgs> ServerDataReceived;

        private void RaiseServerConnected()
        {
            try
            {
                if (ServerConnected != null)
                {
                    ServerConnected(this, new TcpServerConnectedEventArgs(this.RemoteEndPoint, this.LocalEndPoint));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        private void RaiseServerDisconnected()
        {
            try
            {
                if (ServerDisconnected != null)
                {
                    ServerDisconnected(this, new TcpServerDisconnectedEventArgs(this.RemoteEndPoint, this.LocalEndPoint));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        private void RaiseServerDataReceived(byte[] data, int dataOffset, int dataLength)
        {
            try
            {
                if (ServerDataReceived != null)
                {
                    ServerDataReceived(this, new TcpServerDataReceivedEventArgs(this, data, dataOffset, dataLength));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        private static void HandleUserSideError(Exception ex)
        {
            _log.Error(string.Format("Error occurred in user side [{0}].", ex.Message), ex);
        }

        #endregion
    }
}
