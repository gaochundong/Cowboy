using System;

namespace Cowboy.WebSockets
{
    [Serializable]
    public sealed class WebSocketHandshakeException : WebSocketException
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
