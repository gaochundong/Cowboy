namespace Cowboy.Sockets
{
    public abstract class AbstractChainableFrameEncoder : IChainableFrameEncoder
    {
        public IFrameEncoder NextEncoder { get; set; }

        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            OnEncodeFrame(payload, offset, count, out frameBuffer, out frameBufferOffset, out frameBufferLength);

            if (this.NextEncoder != null)
            {
                this.NextEncoder.EncodeFrame(frameBuffer, frameBufferOffset, frameBufferLength, out frameBuffer, out frameBufferOffset, out frameBufferLength);
            }
        }

        protected abstract void OnEncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength);
    }
}
