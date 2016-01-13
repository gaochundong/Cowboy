using System.Text;

namespace Cowboy.Sockets.WebSockets
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

        protected override byte[] BuildFrameArray()
        {
            var data = Encoding.UTF8.GetBytes(Data);
            return Encode(OpCode, data, 0, data.Length, true, IsMasked);
        }
    }
}
