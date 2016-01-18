using System;

namespace Cowboy.WebSockets
{
    public abstract class ControlFrame : Frame
    {
        public override FrameType FrameType { get { return FrameType.Control; } }
    }
}
