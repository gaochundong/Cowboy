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
        Closing = 3,
        Closed = 5,
    }
}
