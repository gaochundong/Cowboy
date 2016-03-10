using System;

namespace Cowboy.WebSockets
{
    public sealed class PongFrame : ControlFrame
    {
        public PongFrame(bool isMasked = true)
        {
            this.IsMasked = isMasked;
        }

        public PongFrame(string data, bool isMasked = true)
            : this(isMasked)
        {
            this.Data = data;
        }

        public string Data { get; private set; }
        public bool IsMasked { get; private set; }

        public override OpCode OpCode
        {
            get { return OpCode.Pong; }
        }

        public byte[] ToArray(IFrameBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            return builder.EncodeFrame(this);
        }
    }
}
