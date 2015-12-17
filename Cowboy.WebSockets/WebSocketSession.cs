using System;
using System.Diagnostics;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public class WebSocketSession
    {
        private HttpListenerContext _httpContext;
        private IBufferManager _bufferManager;

        public WebSocketSession(
            WebSocketModule module,
            HttpListenerContext httpContext,
            WebSocketContext webSocketContext,
            CancellationToken cancellationToken)
        {
            if (module == null)
                throw new ArgumentNullException("module");
            if (httpContext == null)
                throw new ArgumentNullException("httpContext");
            if (webSocketContext == null)
                throw new ArgumentNullException("webSocketContext");

            _httpContext = httpContext;
            this.Module = module;
            this.Context = webSocketContext;
            this.CancellationToken = cancellationToken;
            this.StartTime = DateTime.UtcNow;

            _bufferManager = new GrowingByteBufferManager(100, 1024);
        }

        public WebSocketModule Module { get; private set; }
        public WebSocketContext Context { get; private set; }
        public CancellationToken CancellationToken { get; private set; }
        public DateTime StartTime { get; private set; }
        public IPEndPoint RemoteEndPoint { get { return _httpContext.Request.RemoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return _httpContext.Request.LocalEndPoint; } }

        public async Task Start()
        {
            var webSocket = this.Context.WebSocket;
            byte[] receiveBuffer = _bufferManager.BorrowBuffer();
            byte[] sessionBuffer = _bufferManager.BorrowBuffer();
            int sessionBufferLength = 0;

            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), this.CancellationToken);

                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            {
                                AppendBuffer(receiveBuffer, receiveResult, ref sessionBuffer, ref sessionBufferLength);

                                if (receiveResult.EndOfMessage)
                                {
                                    var message = new WebSocketTextMessage(this, Encoding.UTF8.GetString(sessionBuffer, 0, sessionBufferLength));
                                    await this.Module.ReceiveTextMessage(message);
                                    sessionBufferLength = 0;
                                }
                            }
                            break;
                        case WebSocketMessageType.Binary:
                            {
                                AppendBuffer(receiveBuffer, receiveResult, ref sessionBuffer, ref sessionBufferLength);

                                if (receiveResult.EndOfMessage)
                                {
                                    var message = new WebSocketBinaryMessage(this, sessionBuffer, 0, sessionBufferLength);
                                    await this.Module.ReceiveBinaryMessage(message);
                                    sessionBufferLength = 0;
                                }
                            }
                            break;
                        case WebSocketMessageType.Close:
                            {
                                await Close(
                                    receiveResult.CloseStatus.HasValue ? receiveResult.CloseStatus.Value : WebSocketCloseStatus.NormalClosure,
                                    receiveResult.CloseStatusDescription);
                            }
                            break;
                    }
                }
            }
            finally
            {
                _bufferManager.ReturnBuffer(receiveBuffer);
                _bufferManager.ReturnBuffer(sessionBuffer);

                if (webSocket != null)
                    webSocket.Dispose();
            }
        }

        private void AppendBuffer(byte[] receiveBuffer, WebSocketReceiveResult receiveResult, ref byte[] sessionBuffer, ref int sessionBufferLength)
        {
            while (sessionBufferLength + receiveResult.Count > sessionBuffer.Length)
            {
                byte[] autoExpandedBuffer = new byte[sessionBuffer.Length * 2];
                Array.Copy(sessionBuffer, 0, autoExpandedBuffer, 0, sessionBufferLength);

                var referenceToBuffer = sessionBuffer;
                sessionBuffer = autoExpandedBuffer;
                _bufferManager.ReturnBuffer(referenceToBuffer);
            }

            Array.Copy(receiveBuffer, 0, sessionBuffer, sessionBufferLength, receiveResult.Count);
            sessionBufferLength = sessionBufferLength + receiveResult.Count;
        }

        public async Task Send(string text)
        {
            await this.Context.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, this.CancellationToken);
        }

        public async Task Send(byte[] binary)
        {
            await Send(binary, 0, binary.Length);
        }

        public async Task Send(byte[] binary, int offset, int count)
        {
            await this.Context.WebSocket.SendAsync(new ArraySegment<byte>(binary, offset, count), WebSocketMessageType.Binary, true, this.CancellationToken);
        }

        public async Task Close(WebSocketCloseStatus closeStatus, string closeStatusDescription)
        {
            await this.Context.WebSocket.CloseAsync(closeStatus, closeStatusDescription, this.CancellationToken);
        }
    }
}
