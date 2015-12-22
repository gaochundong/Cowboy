using System;
using System.Net.Sockets;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    internal sealed class TcpSocketSession : ISession
    {
        private static readonly ILog _log = Logger.Get<TcpSocketSession>();
        private readonly object _sync = new object();
        private readonly IBufferManager _bufferManager;

        public TcpSocketSession(TcpClient tcpClient, IBufferManager bufferManager)
        {
            if (tcpClient == null)
                throw new ArgumentNullException("tcpClient");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");

            this.TcpClient = tcpClient;
            _bufferManager = bufferManager;

            this.ReceiveBuffer = _bufferManager.BorrowBuffer();
            this.SessionBuffer = _bufferManager.BorrowBuffer();
            this.SessionBufferCount = 0;

            this.SessionId = Guid.NewGuid();
            if (this.TcpClient.Client.Connected)
            {
                this.SessionKey = this.TcpClient.Client.RemoteEndPoint.ToString();
            }
        }

        public Guid SessionId { get; private set; }
        public string SessionKey { get; private set; }

        public TcpClient TcpClient { get; private set; }
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

        public NetworkStream Stream
        {
            get
            {
                return this.TcpClient.GetStream();
            }
        }

        public bool Connected
        {
            get
            {
                return this.TcpClient.Client.Connected;
            }
        }

        public void Close()
        {
            try
            {
                this.TcpClient.Client.Disconnect(false);
            }
            finally
            {
                _bufferManager.ReturnBuffers(this.ReceiveBuffer, this.SessionBuffer);
            }
        }

        public override string ToString()
        {
            return string.Format("SessionKey[{0}], SessionBufferLength[{1}]", SessionKey, SessionBufferCount);
        }
    }
}
