using System;

namespace Cowboy.Sockets.WebSockets
{
    public class WebSocketTextMessage : EventArgs
    {
        public WebSocketTextMessage(AsyncWebSocketSession session, string text)
        {
            this.Session = session;
            this.Text = text;
        }

        public AsyncWebSocketSession Session { get; private set; }
        public string Text { get; private set; }

        public override string ToString()
        {
            return string.Format("Session[{0}] -> Text[{1}]", this.Session.RemoteEndPoint, this.Text);
        }
    }
}
