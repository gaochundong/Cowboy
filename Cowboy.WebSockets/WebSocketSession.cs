using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public class WebSocketSession
    {
        public WebSocketSession(WebSocketContext context, CancellationToken cancellationToken)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            this.Context = context;
            this.CancellationToken = cancellationToken;
            this.StartTime = DateTime.UtcNow;
        }

        public DateTime StartTime { get; }
        public WebSocketContext Context { get; }
        public CancellationToken CancellationToken { get; }

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
