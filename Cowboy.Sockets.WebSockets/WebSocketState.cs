using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public enum WebSocketState
    {
        None = 0,
        Connecting = 1,
        Open = 2,
        //CloseSent = 3, // WebSocket close handshake started from local endpoint
        //CloseReceived = 4, // WebSocket close message received from remote endpoint.
        Closed = 5,
        //Aborted = 6,
    }
}
