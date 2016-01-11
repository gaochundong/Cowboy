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

        public override FrameOpCode OpCode
        {
            get { return FrameOpCode.Pong; }
        }

        public byte[] ToArray()
        {
            var data = Encoding.UTF8.GetBytes(Data);
            return Encode(OpCode, data, 0, data.Length, IsMasked);
        }
    }
}
