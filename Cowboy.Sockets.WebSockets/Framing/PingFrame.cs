using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class PingFrame : ControlFrame
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

        public override OpCode OpCode
        {
            get { return OpCode.Ping; }
        }

        protected override byte[] BuildFrameArray()
        {
            if (!string.IsNullOrEmpty(Data))
            {
                var data = Encoding.UTF8.GetBytes(Data);
                return Encode(OpCode, data, 0, data.Length, true, IsMasked);
            }
            else
            {
                return Encode(OpCode, new byte[0], 0, 0, true, IsMasked);
            }
        }
    }
}
