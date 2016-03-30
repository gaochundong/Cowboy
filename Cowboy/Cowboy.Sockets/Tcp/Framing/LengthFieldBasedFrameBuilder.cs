using System;

namespace Cowboy.Sockets
{
    public enum LengthFieldLengthType
    {
        OneByte = 1,
        TwoBytes = 2,
        FourBytes = 4,
        EigthBytes = 8,
    }

    public sealed class LengthFieldBasedFrameBuilder : FrameBuilder
    {
        public LengthFieldBasedFrameBuilder(LengthFieldLengthType lengthFieldLength)
            : this(new LengthFieldBasedFrameEncoder(lengthFieldLength), new LengthFieldBasedFrameDecoder(lengthFieldLength))
        {
        }

        public LengthFieldBasedFrameBuilder(LengthFieldBasedFrameEncoder encoder, LengthFieldBasedFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    public sealed class LengthFieldBasedFrameEncoder : IFrameEncoder
    {
        public LengthFieldBasedFrameEncoder(LengthFieldLengthType lengthFieldLength)
        {
            LengthFieldLength = lengthFieldLength;
        }

        public LengthFieldLengthType LengthFieldLength { get; private set; }

        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            byte[] buffer = null;

            switch (this.LengthFieldLength)
            {
                case LengthFieldLengthType.OneByte:
                    {
                        if (count > byte.MaxValue)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        buffer = new byte[1 + count];
                        buffer[0] = (byte)count;
                        Array.Copy(payload, offset, buffer, 1, count);
                    }
                    break;
                case LengthFieldLengthType.TwoBytes:
                    {
                        if (count > short.MaxValue)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        buffer = new byte[2 + count];
                        buffer[0] = (byte)((ushort)count >> 8);
                        buffer[1] = (byte)count;
                        Array.Copy(payload, offset, buffer, 2, count);
                    }
                    break;
                case LengthFieldLengthType.FourBytes:
                    {
                        buffer = new byte[4 + count];
                        uint unsignedValue = (uint)count;
                        buffer[0] = (byte)(unsignedValue >> 24);
                        buffer[1] = (byte)(unsignedValue >> 16);
                        buffer[2] = (byte)(unsignedValue >> 8);
                        buffer[3] = (byte)unsignedValue;
                        Array.Copy(payload, offset, buffer, 4, count);
                    }
                    break;
                case LengthFieldLengthType.EigthBytes:
                    {
                        buffer = new byte[8 + count];
                        ulong unsignedValue = (ulong)count;
                        buffer[0] = (byte)(unsignedValue >> 56);
                        buffer[1] = (byte)(unsignedValue >> 48);
                        buffer[2] = (byte)(unsignedValue >> 40);
                        buffer[3] = (byte)(unsignedValue >> 32);
                        buffer[4] = (byte)(unsignedValue >> 24);
                        buffer[5] = (byte)(unsignedValue >> 16);
                        buffer[6] = (byte)(unsignedValue >> 8);
                        buffer[7] = (byte)unsignedValue;
                        Array.Copy(payload, offset, buffer, 8, count);
                    }
                    break;
                default:
                    throw new NotSupportedException("Specified length field length is not supported.");
            }

            frameBuffer = buffer;
            frameBufferOffset = 0;
            frameBufferLength = buffer.Length;
        }
    }

    public sealed class LengthFieldBasedFrameDecoder : IFrameDecoder
    {
        public LengthFieldBasedFrameDecoder(LengthFieldLengthType lengthFieldLength)
        {
            LengthFieldLength = lengthFieldLength;
        }

        public LengthFieldLengthType LengthFieldLength { get; private set; }

        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            byte[] output = null;
            long length = 0;

            switch (this.LengthFieldLength)
            {
                case LengthFieldLengthType.OneByte:
                    {
                        if (count < 1)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        length = buffer[offset];
                        if (count - 1 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 1, output, 0, length);
                    }
                    break;
                case LengthFieldLengthType.TwoBytes:
                    {
                        if (count < 2)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        length = (short)(buffer[offset] << 8 | buffer[offset + 1]);
                        if (count - 2 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 2, output, 0, length);
                    }
                    break;
                case LengthFieldLengthType.FourBytes:
                    {
                        if (count < 4)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        length = buffer[offset] << 24 |
                            buffer[offset + 1] << 16 |
                            buffer[offset + 2] << 8 |
                            buffer[offset + 3];
                        if (count - 4 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 4, output, 0, length);
                    }
                    break;
                case LengthFieldLengthType.EigthBytes:
                    {
                        if (count < 8)
                        {
                            throw new ArgumentOutOfRangeException("count");
                        }

                        int i1 = buffer[offset] << 24 |
                            buffer[offset + 1] << 16 |
                            buffer[offset + 2] << 8 |
                            buffer[offset + 3];
                        int i2 = buffer[offset + 4] << 24 |
                            buffer[offset + 5] << 16 |
                            buffer[offset + 6] << 8 |
                            buffer[offset + 7];

                        length = (uint)i2 | ((long)i1 << 32);
                        if (count - 8 < length)
                            return false;

                        output = new byte[length];
                        Array.Copy(buffer, offset + 8, output, 0, length);
                    }
                    break;
                default:
                    throw new NotSupportedException("Specified length field length is not supported.");
            }

            payload = output;
            payloadOffset = 0;
            payloadCount = output.Length;

            frameLength = (int)this.LengthFieldLength + output.Length;

            return true;
        }
    }
}
