using System.Text;

namespace Cowboy.WebSockets
{
    internal sealed class Consts
    {
        internal static readonly byte[] HttpMessageTerminator = Encoding.UTF8.GetBytes("\r\n\r\n");

        internal static readonly string[] WebSocketSchemes = new string[] { "ws", "wss" };

        internal const string HttpHeaderLineFormat = "{0}: {1}";

        internal const string HttpStatusCodeName = "HttpStatusCode";
        internal const string HttpStatusCodeDescription = "HttpStatusCodeDescription";
        internal const string HttpGetMethodName = "GET";
        internal const string HttpVersionName = "HTTP";
        internal const string HttpVersion = "1.1";

        internal const string SecWebSocketKeyGuid = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        internal const string WebSocketUpgradeToken = "websocket";
        internal const string WebSocketConnectionToken = "Upgrade";

        // https://www.iana.org/assignments/websocket/websocket.xhtml#version-number
        // Version Number 	Reference 	Status 
        // 0	[draft-ietf-hybi-thewebsocketprotocol-00]	Interim
        // 1	[draft-ietf-hybi-thewebsocketprotocol-01]	Interim
        // 2	[draft-ietf-hybi-thewebsocketprotocol-02]	Interim
        // 3	[draft-ietf-hybi-thewebsocketprotocol-03]	Interim
        // 4	[draft-ietf-hybi-thewebsocketprotocol-04]	Interim
        // 5	[draft-ietf-hybi-thewebsocketprotocol-05]	Interim
        // 6	[draft-ietf-hybi-thewebsocketprotocol-06]	Interim
        // 7	[draft-ietf-hybi-thewebsocketprotocol-07]	Interim
        // 8	[draft-ietf-hybi-thewebsocketprotocol-08]	Interim
        // 9	[Reserved]	
        // 10	[Reserved]	
        // 11	[Reserved]	
        // 12	[Reserved]	
        // 13	[RFC6455]	Standard
        internal const string WebSocketVersion = "13";
    }
}
