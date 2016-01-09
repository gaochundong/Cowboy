using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class PongFrame : ControlFrame
    {
        public override FrameOpCode OpCode
        {
            get { return FrameOpCode.Pong; }
        }
    }
}
