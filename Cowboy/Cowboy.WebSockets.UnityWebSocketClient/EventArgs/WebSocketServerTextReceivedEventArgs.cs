using System;

namespace Cowboy.WebSockets
{
    public class WebSocketServerTextReceivedEventArgs : EventArgs
    {
        public WebSocketServerTextReceivedEventArgs(WebSocketClient client, string text)
        {
            Client = client;
            Text = text;
        }

        public WebSocketClient Client { get; private set; }
        public string Text { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}", this.Client);
        }
    }
}
