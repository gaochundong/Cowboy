namespace Cowboy.WebSockets
{
    // https://www.iana.org/assignments/websocket/websocket.xhtml
    // The opcode denotes the frame type of the WebSocket frame.
    // The opcode is an integer number between 0 and 15, inclusive.
    // Opcode 	Meaning 	Reference 
    // 0	Continuation Frame	[RFC6455]
    // 1	Text Frame	[RFC6455]
    // 2	Binary Frame	[RFC6455]
    // 3-7	Unassigned	
    // 8	Connection Close Frame	[RFC6455]
    // 9	Ping Frame	[RFC6455]
    // 10	Pong Frame	[RFC6455]
    // 11-15	Unassigned
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
