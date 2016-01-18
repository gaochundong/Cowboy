using System;

namespace Cowboy.WebSockets
{
    public sealed class CloseFrame : ControlFrame
    {
        public CloseFrame(bool isMasked = true)
        {
            this.IsMasked = isMasked;
        }

        public CloseFrame(WebSocketCloseCode closeCode, string closeReason, bool isMasked = true)
            : this(isMasked)
        {
            this.CloseCode = closeCode;
            this.CloseReason = closeReason;
        }

        public WebSocketCloseCode CloseCode { get; private set; }
        public string CloseReason { get; private set; }
        public bool IsMasked { get; private set; }

        public override OpCode OpCode
        {
            get { return OpCode.Close; }
        }

        public byte[] ToArray(IFrameBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            return builder.EncodeFrame(this);
        }
    }
}
