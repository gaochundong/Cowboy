using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    internal sealed class PingFrame : ControlFrame
    {
        public PingFrame(bool isMasked = true)
        {
            this.IsMasked = isMasked;
        }

        public PingFrame(string data, bool isMasked = true)
            : this(isMasked)
        {
            this.Data = data;
        }

        public string Data { get; private set; }
        public bool IsMasked { get; private set; }

        public override FrameOpCode OpCode
        {
            get { return FrameOpCode.Ping; }
        }

        public byte[] ToArray()
        {
            if (!string.IsNullOrEmpty(Data))
            {
                var data = Encoding.UTF8.GetBytes(Data);
                return Encode(OpCode, data, 0, data.Length, IsMasked);
            }
            else
            {
                return Encode(OpCode, new byte[0], 0, 0, IsMasked);
            }
        }
    }
}
