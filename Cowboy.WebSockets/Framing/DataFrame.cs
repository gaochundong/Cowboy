namespace Cowboy.WebSockets
{
    public abstract class DataFrame : Frame
    {
        public byte[] ToArray()
        {
            return BuildFrameArray();
        }

        protected abstract byte[] BuildFrameArray();
    }
}
