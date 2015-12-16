using System;

namespace Cowboy.WebSockets
{
    public class WebSocketTextMessage : EventArgs
    {
        public WebSocketTextMessage(WebSocketSession session, string text)
        {
            this.Session = session;
            this.Text = text;
        }

        public WebSocketSession Session { get; private set; }
        public string Text { get; private set; }

        public override string ToString()
        {
            return string.Format("Session[{0}] -> Text[{1}]", this.Session.RemoteEndPoint, this.Text);
        }
    }
}
