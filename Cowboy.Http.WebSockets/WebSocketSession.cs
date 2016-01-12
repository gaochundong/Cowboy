using System;
using System.Collections.Generic;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;
using Cowboy.Logging;

namespace Cowboy.Http.WebSockets
{
    public class WebSocketSession
    {
        private ILog _log = Logger.Get<WebSocketSession>();
        private HttpListenerContext _httpContext;
        private IBufferManager _bufferManager;
        private readonly string _sessionKey;

        public WebSocketSession(
            WebSocketModule module, HttpListenerContext httpContext, WebSocketContext webSocketContext,
            CancellationToken cancellationToken, IBufferManager bufferManager)
            : this(module, httpContext, webSocketContext,
                  cancellationToken, bufferManager, Encoding.UTF8)
        {
        }

        public WebSocketSession(
            WebSocketModule module, HttpListenerContext httpContext, WebSocketContext webSocketContext,
            CancellationToken cancellationToken, IBufferManager bufferManager, Encoding encoding)
        {
            if (module == null)
                throw new ArgumentNullException("module");
            if (httpContext == null)
                throw new ArgumentNullException("httpContext");
            if (webSocketContext == null)
                throw new ArgumentNullException("webSocketContext");
            if (bufferManager == null)
                throw new ArgumentNullException("bufferManager");
            if (encoding == null)
                throw new ArgumentNullException("encoding");

            _httpContext = httpContext;
            this.Module = module;
            this.Context = webSocketContext;
            this.CancellationToken = cancellationToken;
            _bufferManager = bufferManager;
            this.Encoding = encoding;

            _sessionKey = Guid.NewGuid().ToString();
            this.StartTime = DateTime.UtcNow;
        }

        public WebSocketModule Module { get; private set; }
        public WebSocketContext Context { get; private set; }
        public Encoding Encoding { get; private set; }
        public CancellationToken CancellationToken { get; private set; }

        public string SessionKey { get { return _sessionKey; } }
        public DateTime StartTime { get; private set; }

        public Uri RequestUri { get { return this.Context.RequestUri; } }
        public bool IsSecureConnection { get { return this.Context.IsSecureConnection; } }
        public string Origin { get { return this.Context.Origin; } }
        public string SecWebSocketVersion { get { return this.Context.SecWebSocketVersion; } }
        public IEnumerable<string> SecWebSocketProtocols { get { return this.Context.SecWebSocketProtocols; } }

        public IPEndPoint RemoteEndPoint { get { return _httpContext.Request.RemoteEndPoint; } }
        public IPEndPoint LocalEndPoint { get { return _httpContext.Request.LocalEndPoint; } }

        public async Task Start()
        {
            var webSocket = this.Context.WebSocket;
            byte[] receiveBuffer = _bufferManager.BorrowBuffer();
            byte[] sessionBuffer = _bufferManager.BorrowBuffer();
            int sessionBufferCount = 0;

            _log.DebugFormat("Session started for [{0}] on [{1}] in module [{2}] with session count [{3}].",
                this.RemoteEndPoint,
                this.StartTime.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                this.Module.ModuleName,
                this.Module.SessionCount);
            try
            {
                while (webSocket.State == WebSocketState.Open)
                {
                    this.CancellationToken.ThrowIfCancellationRequested();

                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), this.CancellationToken);

                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            {
                                if (receiveResult.EndOfMessage && sessionBufferCount == 0)
                                {
                                    var message = new WebSocketTextMessage(this, this.Encoding.GetString(receiveBuffer, 0, receiveResult.Count));
                                    await this.Module.ReceiveTextMessage(message);
                                }
                                else
                                {
                                    AppendBuffer(receiveBuffer, receiveResult.Count, ref sessionBuffer, ref sessionBufferCount);

                                    if (receiveResult.EndOfMessage)
                                    {
                                        var message = new WebSocketTextMessage(this, this.Encoding.GetString(sessionBuffer, 0, sessionBufferCount));
                                        await this.Module.ReceiveTextMessage(message);
                                        sessionBufferCount = 0;
                                    }
                                }
                            }
                            break;
                        case WebSocketMessageType.Binary:
                            {
                                if (receiveResult.EndOfMessage && sessionBufferCount == 0)
                                {
                                    var message = new WebSocketBinaryMessage(this, receiveBuffer, 0, receiveResult.Count);
                                    await this.Module.ReceiveBinaryMessage(message);
                                }
                                else
                                {
                                    AppendBuffer(receiveBuffer, receiveResult.Count, ref sessionBuffer, ref sessionBufferCount);

                                    if (receiveResult.EndOfMessage)
                                    {
                                        var message = new WebSocketBinaryMessage(this, sessionBuffer, 0, sessionBufferCount);
                                        await this.Module.ReceiveBinaryMessage(message);
                                        sessionBufferCount = 0;
                                    }
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
            catch (WebSocketException) { }
            finally
            {
                _bufferManager.ReturnBuffer(receiveBuffer);
                _bufferManager.ReturnBuffer(sessionBuffer);

                _log.DebugFormat("Session closed for [{0}] on [{1}] in module [{2}] with session count [{3}].",
                    this.RemoteEndPoint,
                    DateTime.UtcNow.ToString(@"yyyy-MM-dd HH:mm:ss.fffffff"),
                    this.Module.ModuleName,
                    this.Module.SessionCount - 1);

                if (webSocket != null)
                    webSocket.Dispose();
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
