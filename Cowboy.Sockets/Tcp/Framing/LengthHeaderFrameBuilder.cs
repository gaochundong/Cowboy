namespace Cowboy.Sockets
{
    public class LengthHeaderFrameBuilder : IFrameBuilder
    {
        public LengthHeaderFrameBuilder(bool isMasked = false)
        {
            IsMasked = isMasked;
        }

        public bool IsMasked { get; private set; }

        public byte[] EncodeFrame(byte[] payload, int offset, int count)
        {
            return Frame.Encode(payload, offset, count, IsMasked);
        }

        public bool TryDecodeFrame(byte[] buffer, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            var frameHeader = Frame.DecodeHeader(buffer, count);
            if (frameHeader != null && frameHeader.Length + frameHeader.PayloadLength <= count)
            {
                if (IsMasked)
                {
                    payload = Frame.DecodeMaskedPayload(buffer, frameHeader.MaskingKeyOffset, frameHeader.Length, frameHeader.PayloadLength);
                    payloadOffset = 0;
                    payloadCount = payload.Length;
                }
                else
                {
                    payload = buffer;
                    payloadOffset = frameHeader.Length;
                    payloadCount = frameHeader.PayloadLength;
                }

                frameLength = frameHeader.Length + frameHeader.PayloadLength;

                return true;
            }

            return false;
        }
    }
}
