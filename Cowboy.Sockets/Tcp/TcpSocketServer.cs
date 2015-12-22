using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public class TcpSocketServer : IDisposable
    {
        #region Fields

        private static readonly ILog _log = Logger.Get<TcpSocketServer>();
        private IBufferManager _bufferManager;
        private TcpListener _listener;
        private ConcurrentDictionary<string, TcpSocketSession> _sessions = new ConcurrentDictionary<string, TcpSocketSession>();
        private bool _disposed = false;

        #endregion

        #region Constructors

        public TcpSocketServer(int listenPort)
            : this(IPAddress.Any, listenPort)
        {
        }

        public TcpSocketServer(IPAddress localIPAddress, int listenPort)
            : this(new IPEndPoint(localIPAddress, listenPort))
        {
        }

        public TcpSocketServer(IPEndPoint localEP)
        {
            if (localEP == null)
                throw new ArgumentNullException("localEP");

            LocalEndPoint = localEP;

            _bufferManager = new GrowingByteBufferManager(100, 64);

            _listener = new TcpListener(LocalEndPoint);
            _listener.AllowNatTraversal(true);

            SetOptions();
        }

        private void SetOptions()
        {
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            _listener.Server.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);
        }

        #endregion

        #region Properties

        public bool IsListening { get; private set; }
        public IPEndPoint LocalEndPoint { get; private set; }
        public bool IsPackingEnabled { get; set; }
        public int SessionCount { get { return _sessions.Count; } }

        #endregion

        #region Server

        public TcpSocketServer Start()
        {
            // default 200 pending connections are waiting in the backlog
            return Start(200);
        }

        public TcpSocketServer Start(int backlog)
        {
            if (backlog > (int)SocketOptionName.MaxConnections || backlog < 0)
            {
                throw new ArgumentOutOfRangeException("backlog");
            }

            if (IsListening) return this;

            IsListening = true;

            _listener.Start(backlog);
            ContinueAcceptSession(_listener);

            return this;
        }

        public TcpSocketServer Stop()
        {
            if (!IsListening) return this;

            try
            {
                _listener.Stop();

                foreach (var session in _sessions.Values)
                {
                    session.Close();
                }
                _sessions.Clear();
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException
                    || ex is SocketException)
                {
                    _log.Error(ex.Message, ex);
                }
                else throw;
            }

            IsListening = false;

            return this;
        }

        public bool Pending()
        {
            if (!IsListening)
                throw new InvalidOperationException("The TCP server is not running.");

            // determine if there are pending connection requests.
            return _listener.Server.Poll(0, SelectMode.SelectRead);
        }

        private void ContinueAcceptSession(TcpListener listener)
        {
            try
            {
                listener.BeginAcceptTcpClient(new AsyncCallback(HandleTcpClientAccepted), listener);
            }
            catch (Exception ex)
            {
                if (ex is ObjectDisposedException
                    || ex is SocketException)
                {
                    _log.Error(ex.Message, ex);
                }
                else throw;
            }
        }

        private void CloseSession(TcpSocketSession session)
        {
            TcpSocketSession sessionToBeThrowAway;
            _sessions.TryRemove(session.SessionKey, out sessionToBeThrowAway);

            try
            {
                if (session != null)
                {
                    session.Close();
                }
            }
            catch { }

            RaiseClientDisconnected(session);
        }

        #endregion

        #region Receive

        private void HandleTcpClientAccepted(IAsyncResult ar)
        {
            if (!IsListening) return;

            TcpListener listener = (TcpListener)ar.AsyncState;

            TcpClient tcpClient = listener.EndAcceptTcpClient(ar);
            if (!tcpClient.Connected) return;

            // set port reuse
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ExclusiveAddressUse, true);
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, false);
            tcpClient.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.NoDelay, 1);

            // create session
            var session = new TcpSocketSession(tcpClient, _bufferManager);

            // add client connection to cache
            _sessions.AddOrUpdate(session.SessionKey, session, (n, o) => { return o; });
            RaiseClientConnected(session);

            // begin to read data
            ContinueReadBuffer(session);

            // keep listening to accept next connection
            ContinueAcceptSession(listener);
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
                if (!ShouldCloseSession(ex, session))
                    throw;
            }
        }

        private void HandleDataReceived(IAsyncResult ar)
        {
            if (!IsListening) return;

            try
            {
                TcpSocketSession session = (TcpSocketSession)ar.AsyncState;
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
                    CloseSession(session);
                    return;
                }

                // received bytes and trigger notifications
                ReceiveBuffer(session, numberOfReadBytes);

                // continue listening for TCP data packets
                ContinueReadBuffer(session);
            }
            catch (Exception ex)
            {
                if (!ShouldCloseSession(ex, (TcpSocketSession)ar.AsyncState))
                    throw;
            }
        }

        private void ReceiveBuffer(TcpSocketSession session, int receivedBufferCount)
        {
            if (!IsPackingEnabled)
            {
                // in the scenario, actually we don't know the length of the message packet, so just guess.
                byte[] receivedBytes = new byte[receivedBufferCount];
                System.Buffer.BlockCopy(session.ReceiveBuffer, 0, receivedBytes, 0, receivedBufferCount);

                // yeah, we received the buffer and then raise it to user side to handle.
                RaiseDataReceived(session, receivedBytes, 0, receivedBufferCount);
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
                session.AppendBuffer(receivedBufferCount);
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

        private void GuardRunning()
        {
            if (!IsListening)
                throw new InvalidProgramException("This TCP server has not been started yet.");
        }

        public void SendTo(ISession session, byte[] data)
        {
            GuardRunning();

            if (session == null)
                throw new ArgumentNullException("session");

            if (data == null)
                throw new ArgumentNullException("data");

            TcpSocketSession writeSession = null;
            if (!_sessions.TryGetValue(session.SessionKey, out writeSession)) return;

            try
            {
                if (writeSession.Stream.CanWrite)
                {
                    if (!IsPackingEnabled)
                    {
                        writeSession.Stream.BeginWrite(data, 0, data.Length, HandleDataWritten, session);
                    }
                    else
                    {
                        var packet = TcpPacket.FromPayload(data);
                        var packetArray = packet.ToArray();
                        writeSession.Stream.BeginWrite(packetArray, 0, packetArray.Length, HandleDataWritten, session);
                    }
                }
            }
            catch (Exception ex)
            {
                if (!ShouldCloseSession(ex, writeSession))
                    throw;
            }
        }

        public void SendToAll(byte[] data)
        {
            GuardRunning();

            foreach (var session in _sessions.Values)
            {
                SendTo(session, data);
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
                if (!ShouldCloseSession(ex, (TcpSocketSession)ar.AsyncState))
                    throw;
            }
        }

        private bool ShouldCloseSession(Exception ex, TcpSocketSession session)
        {
            if (ex is ObjectDisposedException
                || ex is InvalidOperationException
                || ex is IOException)
            {
                _log.Error(ex.Message, ex);

                // connection has been closed
                CloseSession(session);

                return true;
            }

            return false;
        }

        #endregion

        #region Events

        public event EventHandler<TcpClientConnectedEventArgs> ClientConnected;
        public event EventHandler<TcpClientDisconnectedEventArgs> ClientDisconnected;
        public event EventHandler<TcpDataReceivedEventArgs> DataReceived;

        private void RaiseClientConnected(ISession session)
        {
            try
            {
                if (ClientConnected != null)
                {
                    ClientConnected(this, new TcpClientConnectedEventArgs(session));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        private void RaiseClientDisconnected(ISession session)
        {
            try
            {
                if (ClientDisconnected != null)
                {
                    ClientDisconnected(this, new TcpClientDisconnectedEventArgs(session));
                }
            }
            catch (Exception ex)
            {
                HandleUserSideError(ex);
            }
        }

        private void RaiseDataReceived(ISession sender, byte[] data, int dataOffset, int dataLength)
        {
            try
            {
                if (DataReceived != null)
                {
                    DataReceived(this, new TcpDataReceivedEventArgs(sender, data, dataOffset, dataLength));
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

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param name="disposing">
        /// <c>true</c> to release both managed and unmanaged resources; 
        /// <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    try
                    {
                        Stop();

                        if (_listener != null)
                        {
                            _listener = null;
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
