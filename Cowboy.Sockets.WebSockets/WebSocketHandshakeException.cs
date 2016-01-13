using System;

namespace Cowboy.WebSockets
{
    public class WebSocketHandshakeException : WebSocketException
    {
        public WebSocketHandshakeException(string message)
            : base(message)
        {
        }

        public WebSocketHandshakeException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
