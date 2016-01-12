using System;

namespace Cowboy.Sockets.WebSockets
{
    public class WebSocketBinaryMessage : EventArgs
    {
        public WebSocketBinaryMessage(AsyncWebSocketSession session, byte[] buffer)
            : this(session, buffer, 0, buffer.Length)
        {
        }

        public WebSocketBinaryMessage(AsyncWebSocketSession session, byte[] buffer, int offset, int count)
        {
            this.Session = session;
            this.Buffer = buffer;
            this.Offset = offset;
            this.Count = count;
        }

        public AsyncWebSocketSession Session { get; private set; }
        public byte[] Buffer { get; private set; }
        public int Offset { get; private set; }
        public int Count { get; private set; }

        public override string ToString()
        {
            return string.Format("Session[{0}] -> BinaryLength[{1}]", this.Session.RemoteEndPoint, this.Count);
        }
    }
}
