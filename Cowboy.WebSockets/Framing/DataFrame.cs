namespace Cowboy.WebSockets
{
    public abstract class DataFrame : Frame
    {
        public override FrameType FrameType { get { return FrameType.Data; } }
    }
}
