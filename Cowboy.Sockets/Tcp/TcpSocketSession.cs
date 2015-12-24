using System;
using System.Net;
using System.Net.Sockets;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public sealed class TcpSocketSession
    {
        private static readonly ILog _log = Logger.Get<TcpSocketSession>();
        private readonly object _sync = new object();
        private readonly IBufferManager _bufferManager;
        private readonly TcpClient _tcpClient;
        private readonly string _sessionKey;

        public TcpSocketSession(TcpClient tcpClient, IBufferManager bufferManager)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");

            _tcpClient = tcpClient;
            _bufferManager = bufferManager;

            this.ReceiveBuffer = _bufferManager.BorrowBuffer();
            this.SessionBuffer = _bufferManager.BorrowBuffer();
            this.SessionBufferCount = 0;

            _sessionKey = Guid.NewGuid().ToString();
        }

        public string SessionKey { get { return _sessionKey; } }

        public NetworkStream Stream { get { return _tcpClient.GetStream(); } }
        public EndPoint RemoteEndPoint { get { return _tcpClient.Client.RemoteEndPoint; } }
        public EndPoint LocalEndPoint { get { return _tcpClient.Client.LocalEndPoint; } }
        public bool Connected { get { return _tcpClient.Client.Connected; } }

        public byte[] ReceiveBuffer { get; private set; }
        public byte[] SessionBuffer { get; private set; }
        public int SessionBufferCount { get; private set; }

        public void AppendBuffer(int appendedCount)
        {
            if (appendedCount <= 0) return;

            lock (_sync)
            {
                if (this.SessionBuffer.Length < (this.SessionBufferCount + appendedCount))
                {
                    byte[] autoExpandedBuffer = _bufferManager.BorrowBuffer();
                    if (autoExpandedBuffer.Length < (this.SessionBufferCount + appendedCount) * 2)
                    {
                        _bufferManager.ReturnBuffer(autoExpandedBuffer);
                        autoExpandedBuffer = new byte[(this.SessionBufferCount + appendedCount) * 2];
                    }

                    Array.Copy(this.SessionBuffer, 0, autoExpandedBuffer, 0, this.SessionBufferCount);

                    var discardBuffer = this.SessionBuffer;
                    this.SessionBuffer = autoExpandedBuffer;
                    _bufferManager.ReturnBuffer(discardBuffer);
                }

                Array.Copy(this.ReceiveBuffer, 0, this.SessionBuffer, this.SessionBufferCount, appendedCount);
                this.SessionBufferCount = this.SessionBufferCount + appendedCount;
            }
        }

        public void ShiftBuffer(int shiftStart)
        {
            lock (_sync)
            {
                if ((this.SessionBufferCount - shiftStart) < shiftStart)
                {
                    Array.Copy(this.SessionBuffer, shiftStart, this.SessionBuffer, 0, this.SessionBufferCount - shiftStart);
                    this.SessionBufferCount = this.SessionBufferCount - shiftStart;
                }
                else
                {
                    byte[] copyBuffer = _bufferManager.BorrowBuffer();
                    if (copyBuffer.Length < (this.SessionBufferCount - shiftStart))
                    {
                        _bufferManager.ReturnBuffer(copyBuffer);
                        copyBuffer = new byte[this.SessionBufferCount - shiftStart];
                    }

                    Array.Copy(this.SessionBuffer, shiftStart, copyBuffer, 0, this.SessionBufferCount - shiftStart);
                    Array.Copy(copyBuffer, 0, this.SessionBuffer, 0, this.SessionBufferCount - shiftStart);
                    this.SessionBufferCount = this.SessionBufferCount - shiftStart;

                    _bufferManager.ReturnBuffer(copyBuffer);
                }
            }
        }

        public void Close()
        {
            try
            {
                _tcpClient.Client.Disconnect(false);
            }
            finally
            {
                _bufferManager.ReturnBuffers(this.ReceiveBuffer, this.SessionBuffer);
            }
        }

        public override string ToString()
        {
            return _sessionKey;
        }
    }
}
