namespace Cowboy.Sockets.WebSockets
{
    public enum WebSocketState
    {
        None = 0,
        Connecting = 1,
        Open = 2,
        Closing = 3,
        Closed = 5,
    }
}
