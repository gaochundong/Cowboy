namespace Cowboy.Sockets.WebSockets
{
    // The opcode denotes the frame type of the WebSocket frame.
    // The opcode is an integer number between 0 and 15, inclusive.
    public enum OpCode : byte
    {
        Continuation = 0,
        Text = 1,
        Binary = 2,
        Close = 8,
        Ping = 9,
        Pong = 10,
    }
}
