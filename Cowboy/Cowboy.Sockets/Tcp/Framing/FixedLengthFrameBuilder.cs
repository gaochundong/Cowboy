using System;

namespace Cowboy.Sockets
{
    public sealed class FixedLengthFrameBuilder : IFrameBuilder
    {
        private readonly int _fixedFrameLength;

        public FixedLengthFrameBuilder(int fixedFrameLength)
        {
            if (fixedFrameLength <= 0)
                throw new ArgumentOutOfRangeException("fixedFrameLength");
            _fixedFrameLength = fixedFrameLength;
        }

        public int FixedFrameLength { get { return _fixedFrameLength; } }

        public byte[] EncodeFrame(byte[] payload, int offset, int count)
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
            return buffer;
        }

        public bool TryDecodeFrame(byte[] buffer, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count < FixedFrameLength)
                return false;

            frameLength = FixedFrameLength;
            payload = buffer;
            payloadOffset = 0;
            payloadCount = FixedFrameLength;
            return true;
        }
    }
}
