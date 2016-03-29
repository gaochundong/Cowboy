namespace Cowboy.Sockets
{
    public abstract class AbstractChainableFrameDecoder : IChainableFrameDecoder
    {
        public IFrameDecoder NextDecoder { get; set; }

        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            var result = OnTryDecodeFrame(buffer, offset, count, out frameLength, out payload, out payloadOffset, out payloadCount);

            if (this.NextDecoder != null)
            {
                return this.NextDecoder.TryDecodeFrame(payload, payloadOffset, payloadCount, out frameLength, out payload, out payloadOffset, out payloadCount);
            }

            return result;
        }

        protected abstract bool OnTryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount);
    }
}
