using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public class TcpSocketClient : IDisposable
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketClient>();
        private IBufferManager _bufferManager;
        private TcpClient _tcpClient;
        private TcpSocketSession _session;
        private bool _disposed = false;
        private readonly TcpSocketClientConfiguration _configuration;
        private readonly IPEndPoint _remoteEndPoint;
        private readonly IPEndPoint _localEndPoint;

        #endregion

        #region Constructors

        public TcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPAddress localIPAddress, int localPort, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), new IPEndPoint(localIPAddress, localPort))
        {
        }

        public TcpSocketClient(IPAddress remoteIPAddress, int remotePort, IPEndPoint localEP = null, TcpSocketClientConfiguration configuration = null)
            : this(new IPEndPoint(remoteIPAddress, remotePort), localEP)
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
            if (_localEndPoint != null)
            {
                _tcpClient = new TcpClient(_localEndPoint);
            }
            else
            {
                _tcpClient = new TcpClient();
            }

            _bufferManager = new GrowingByteBufferManager(_configuration.InitialBufferAllocationCount, _configuration.ReceiveBufferSize);
            _session = new TcpSocketSession(_tcpClient, _bufferManager);
        }

        #endregion

        #region Properties

        public bool Connected { get { return _session != null && _session.Connected; } }
        public EndPoint RemoteEndPoint { get { return _session.RemoteEndPoint; } }
        public EndPoint LocalEndPoint { get { return _session.LocalEndPoint; } }

        #endregion

        #region Connect

        public TcpSocketClient Connect()
        {
            if (!Connected)
            {
                _session.TcpClient.Client.BeginConnect(_remoteEndPoint, HandleTcpServerConnected, _session);
            }

            return this;
        }

        public TcpSocketClient Close()
        {
            if (Connected)
            {
                _session.Close();
            }

            RaiseServerDisconnected();

            return this;
        }

        #endregion

        #region Receive

        private void HandleTcpServerConnected(IAsyncResult ar)
        {
            try
            {
                var session = (TcpSocketSession)ar.AsyncState;
                session.TcpClient.Client.EndConnect(ar);
                RaiseServerConnected();

                // we are connected successfully and start async read operation.
                ContinueReadBuffer(session);
            }
            catch (Exception ex)
            {
                _log.Error(ex.Message, ex);
            }
        }

        private void ContinueReadBuffer(TcpSocketSession session)
        {
            try
            {
                // buffer : An array of type Byte that is the location in memory to store data read from the NetworkStream.
                // offset : The location in buffer to begin storing the data.
                // size : The number of bytes to read from the NetworkStream.
                // callback : The AsyncCallback delegate that is executed when BeginRead completes.
                // state : An object that contains any additional user-defined data.
                session.Stream.BeginRead(session.ReceiveBuffer, 0, session.ReceiveBuffer.Length, HandleDataReceived, session);
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
                var session = (TcpSocketSession)ar.AsyncState;
                if (!session.Connected) return;

                int numberOfReadBytes = 0;
                try
                {
                    // The EndRead method blocks until data is available. The EndRead method reads 
                    // as much data as is available up to the number of bytes specified in the size 
                    // parameter of the BeginRead method. If the remote host shuts down the Socket 
                    // connection and all available data has been received, the EndRead method 
                    // completes immediately and returns zero bytes.
                    numberOfReadBytes = session.Stream.EndRead(ar);
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
                ReceiveBuffer(session, numberOfReadBytes);

                // then start reading from the network again
                ContinueReadBuffer(session);
            }
            catch (Exception ex)
            {
                if (!ShouldClose(ex))
                    throw;
            }
        }

        private void ReceiveBuffer(TcpSocketSession session, int receivedBufferLength)
        {
            if (!_configuration.IsPackingEnabled)
            {
                // in the scenario, actually we don't know the length of the message packet, so just guess.
                byte[] receivedBytes = new byte[receivedBufferLength];
                System.Buffer.BlockCopy(session.ReceiveBuffer, 0, receivedBytes, 0, receivedBufferLength);

                // yeah, we received the buffer and then raise it to user side to handle.
                RaiseDataReceived(session, receivedBytes, 0, receivedBufferLength);
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
                session.AppendBuffer(receivedBufferLength);
                while (true)
                {
                    var packetHeader = TcpPacketHeader.ReadHeader(session.SessionBuffer);
                    if (TcpPacketHeader.HEADER_SIZE + packetHeader.PayloadSize <= session.SessionBufferCount)
                    {
                        RaiseReceivedBuffer(session, packetHeader.PayloadSize);
                    }
                    else
                    {
                        break;
                    }
                }
            }
        }

        private void RaiseReceivedBuffer(TcpSocketSession session, int payloadLength)
        {
            // yeah, we received the buffer and then raise it to user side to handle.
            RaiseDataReceived(session, session.SessionBuffer, TcpPacketHeader.HEADER_SIZE, payloadLength);

            // remove the received packet from buffer
            session.ShiftBuffer(TcpPacketHeader.HEADER_SIZE + payloadLength);
        }

        #endregion

        #region Send

        public void Send(byte[] data)
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
                if (!_configuration.IsPackingEnabled)
                {
                    _session.Stream.BeginWrite(data, 0, data.Length, HandleDataWritten, _session);
                }
                else
                {
                    var packet = TcpPacket.FromPayload(data);
                    var packetArray = packet.ToArray();
                    _session.Stream.BeginWrite(packetArray, 0, packetArray.Length, HandleDataWritten, _session);
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
                ((TcpSocketSession)ar.AsyncState).Stream.EndWrite(ar);
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
        public event EventHandler<TcpDataReceivedEventArgs> DataReceived;

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

        private void RaiseDataReceived(TcpSocketSession session, byte[] data, int dataOffset, int dataLength)
        {
            try
            {
                if (DataReceived != null)
                {
                    DataReceived(this, new TcpDataReceivedEventArgs(session, data, dataOffset, dataLength));
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

        #region IDisposable Members

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Close();

                        if (_tcpClient != null)
                        {
                            _tcpClient = null;
                        }

                        if (_session != null)
                        {
                            _session = null;
                        }
                    }
                    catch (SocketException ex)
                    {
                        _log.Error(ex.Message, ex);
                    }
                }

                _disposed = true;
            }
        }

        #endregion
    }
}
