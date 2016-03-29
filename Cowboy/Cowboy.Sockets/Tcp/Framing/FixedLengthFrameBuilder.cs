using System;

namespace Cowboy.Sockets
{
    public sealed class FixedLengthFrameBuilder : IFrameBuilder
    {
        public FixedLengthFrameBuilder(int fixedFrameLength)
            : this(new FixedLengthFrameEncoder(fixedFrameLength), new FixedLengthFrameDecoder(fixedFrameLength))
        {
        }

        public FixedLengthFrameBuilder(IFrameEncoder encoder, IFrameDecoder decoder)
        {
            if (encoder == null)
                throw new ArgumentNullException("encoder");
            if (decoder == null)
                throw new ArgumentNullException("decoder");

            this.Encoder = encoder;
            this.Decoder = decoder;
        }

        public IFrameEncoder Encoder { get; private set; }
        public IFrameDecoder Decoder { get; private set; }
    }

    public sealed class FixedLengthFrameEncoder : AbstractChainableFrameEncoder
    {
        private readonly int _fixedFrameLength;

        public FixedLengthFrameEncoder(int fixedFrameLength)
        {
            if (fixedFrameLength <= 0)
                throw new ArgumentOutOfRangeException("fixedFrameLength");
            _fixedFrameLength = fixedFrameLength;
        }

        public int FixedFrameLength { get { return _fixedFrameLength; } }

        protected override void OnEncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            if (count == FixedFrameLength)
            {
                frameBuffer = payload;
                frameBufferOffset = offset;
                frameBufferLength = count;
            }
            else
            {
                var buffer = new byte[FixedFrameLength];
                if (count >= FixedFrameLength)
                {
                    Array.Copy(payload, offset, buffer, 0, FixedFrameLength);
                }
                else
                {
                    Array.Copy(payload, offset, buffer, 0, count);
                    for (int i = 0; i < FixedFrameLength - count; i++)
                    {
                        buffer[count + i] = (byte)'\n';
                    }
                }

                frameBuffer = buffer;
                frameBufferOffset = 0;
                frameBufferLength = buffer.Length;
            }
        }
    }

    public sealed class FixedLengthFrameDecoder : AbstractChainableFrameDecoder
    {
        private readonly int _fixedFrameLength;

        public FixedLengthFrameDecoder(int fixedFrameLength)
        {
            if (fixedFrameLength <= 0)
                throw new ArgumentOutOfRangeException("fixedFrameLength");
            _fixedFrameLength = fixedFrameLength;
        }

        public int FixedFrameLength { get { return _fixedFrameLength; } }

        protected override bool OnTryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count < FixedFrameLength)
                return false;

            frameLength = FixedFrameLength;
            payload = buffer;
            payloadOffset = offset;
            payloadCount = FixedFrameLength;
            return true;
        }
    }
}
