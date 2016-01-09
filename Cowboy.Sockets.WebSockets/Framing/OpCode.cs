namespace Cowboy.Sockets.WebSockets
{
    // The opcode denotes the frame type of the WebSocket frame.
    // The opcode is an integer number between 0 and 15, inclusive.
    public class OpCode
    {
        public const int Continuation = 0;
        public const int Text = 1;
        public const int Binary = 2;
        public const int Close = 8;
        public const int Ping = 9;
        public const int Pong = 10;
    }
}
