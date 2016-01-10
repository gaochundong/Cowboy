using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class PingFrame : ControlFrame
    {
        public PingFrame()
        {
        }

        public PingFrame(string applicationData)
            : this()
        {
            this.ApplicationData = applicationData;
        }

        public string ApplicationData { get; private set; }

        public override FrameOpCode OpCode
        {
            get { return FrameOpCode.Ping; }
        }

        public byte[] ToArray()
        {
            var data = Encoding.UTF8.GetBytes(ApplicationData);
            return Encode(OpCode, data, 0, data.Length);
        }
    }
}
