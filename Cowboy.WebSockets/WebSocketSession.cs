using System;
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

        public WebSocketSession(
            HttpListenerContext httpContext,
            HttpListenerWebSocketContext webSocketContext,
            CancellationToken cancellationToken)
        {
            if (httpContext == null)
                throw new ArgumentNullException("httpContext");
            if (webSocketContext == null)
                throw new ArgumentNullException("webSocketContext");

            _httpContext = httpContext;
            this.Context = webSocketContext;
            this.CancellationToken = cancellationToken;
            this.StartTime = DateTime.UtcNow;
        }

        public IPEndPoint RemoteEndPoint
        {
            get
            {
                return _httpContext.Request.RemoteEndPoint;
            }
        }
        public IPEndPoint LocalEndPoint
        {
            get
            {
                return _httpContext.Request.LocalEndPoint;
            }
        }

        public HttpListenerWebSocketContext Context { get; }
        public CancellationToken CancellationToken { get; }
        public DateTime StartTime { get; }

        public async Task Start()
        {
            var webSocket = this.Context.WebSocket;

            try
            {
                byte[] receiveBuffer = new byte[1024];

                while (webSocket.State == WebSocketState.Open)
                {
                    var receiveResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(receiveBuffer), this.CancellationToken);

                    switch (receiveResult.MessageType)
                    {
                        case WebSocketMessageType.Text:
                            break;
                        case WebSocketMessageType.Binary:
                            {
                                Console.WriteLine("Binary Received: {0}", Encoding.UTF8.GetString(receiveBuffer, 0, receiveResult.Count));
                                await webSocket.SendAsync(new ArraySegment<byte>(receiveBuffer, 0, receiveResult.Count), WebSocketMessageType.Binary, receiveResult.EndOfMessage, this.CancellationToken);
                            }
                            break;
                        case WebSocketMessageType.Close:
                            {
                                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "", this.CancellationToken);
                            }
                            break;
                    }
                }
            }
            finally
            {
                if (webSocket != null)
                    webSocket.Dispose();
            }
        }
    }
}
