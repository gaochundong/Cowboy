using System;

namespace Cowboy.Sockets.WebSockets
{
    public class WebSocketException : Exception
    {
        public WebSocketException(string message)
            : base(message)
        {
        }

        public WebSocketException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
