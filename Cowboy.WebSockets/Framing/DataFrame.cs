namespace Cowboy.WebSockets
{
    public abstract class DataFrame : Frame
    {
        public override FrameType FrameType { get { return FrameType.Data; } }

        public byte[] ToArray()
        {
            return BuildFrameArray();
        }

        protected abstract byte[] BuildFrameArray();
    }
}
