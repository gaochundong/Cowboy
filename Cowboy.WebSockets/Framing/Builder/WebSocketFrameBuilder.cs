namespace Cowboy.WebSockets
{
    public class WebSocketFrameBuilder : IFrameBuilder
    {
        public WebSocketFrameBuilder()
        {
        }

        public byte[] EncodeFrame(byte[] payload, int offset, int count, OpCode opCode, bool isFinal, bool isMasked)
        {
            return Frame.Encode(opCode, payload, offset, count, isFinal, isMasked);
        }

        public bool TryDecodeFrame(byte[] buffer, int count, bool isMasked, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            var frameHeader = Frame.DecodeHeader(buffer, count);
            if (frameHeader != null && frameHeader.Length + frameHeader.PayloadLength <= count)
            {
                if (isMasked)
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
