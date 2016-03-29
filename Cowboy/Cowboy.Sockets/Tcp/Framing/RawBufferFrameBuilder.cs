using System;

namespace Cowboy.Sockets
{
    public sealed class RawBufferFrameBuilder : FrameBuilder
    {
        public RawBufferFrameBuilder()
            : this(new RawBufferFrameEncoder(), new RawBufferFrameDecoder())
        {
        }

        public RawBufferFrameBuilder(RawBufferFrameEncoder encoder, RawBufferFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    public sealed class RawBufferFrameEncoder : IFrameEncoder
    {
        public RawBufferFrameEncoder()
        {
        }

        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            frameBuffer = payload;
            frameBufferOffset = offset;
            frameBufferLength = count;
        }
    }

    public sealed class RawBufferFrameDecoder : IFrameDecoder
    {
        public RawBufferFrameDecoder()
        {
        }

        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count <= 0)
                return false;

            frameLength = count;
            payload = buffer;
            payloadOffset = offset;
            payloadCount = count;
            return true;
        }
    }
}
