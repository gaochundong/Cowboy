using System;
using System.Text;

namespace Cowboy.Sockets
{
    public class LineDelimiter : IEquatable<LineDelimiter>
    {
        public static readonly LineDelimiter CRLF = new LineDelimiter("\r\n");
        public static readonly LineDelimiter UNIX = new LineDelimiter("\n");
        public static readonly LineDelimiter MAC = new LineDelimiter("\r");
        public static readonly LineDelimiter WINDOWS = CRLF;

        public LineDelimiter(string delimiter)
        {
            this.DelimiterString = delimiter;
            this.DelimiterChars = this.DelimiterString.ToCharArray();
            this.DelimiterBytes = Encoding.UTF8.GetBytes(this.DelimiterChars);
        }

        public string DelimiterString { get; private set; }
        public char[] DelimiterChars { get; private set; }
        public byte[] DelimiterBytes { get; private set; }

        public bool Equals(LineDelimiter other)
        {
            if (Object.ReferenceEquals(other, null)) return false;
            if (Object.ReferenceEquals(this, other)) return true;

            return (StringComparer.OrdinalIgnoreCase.Compare(this.DelimiterString, other.DelimiterString) == 0);
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as LineDelimiter);
        }

        public override int GetHashCode()
        {
            return this.DelimiterString.GetHashCode();
        }

        public override string ToString()
        {
            return this.DelimiterString;
        }
    }

    public sealed class LineBasedFrameBuilder : FrameBuilder
    {
        public LineBasedFrameBuilder()
            : this(new LineBasedFrameEncoder(), new LineBasedFrameDecoder())
        {
        }

        public LineBasedFrameBuilder(LineDelimiter delimiter)
            : this(new LineBasedFrameEncoder(delimiter), new LineBasedFrameDecoder(delimiter))
        {
        }

        public LineBasedFrameBuilder(LineBasedFrameEncoder encoder, LineBasedFrameDecoder decoder)
            : base(encoder, decoder)
        {
        }
    }

    public sealed class LineBasedFrameEncoder : IFrameEncoder
    {
        private readonly LineDelimiter _delimiter;

        public LineBasedFrameEncoder()
            : this(LineDelimiter.CRLF)
        {
        }

        public LineBasedFrameEncoder(LineDelimiter delimiter)
        {
            if (delimiter == null)
                throw new ArgumentNullException("delimiter");
            _delimiter = delimiter;
        }

        public LineDelimiter LineDelimiter { get { return _delimiter; } }

        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            var buffer = new byte[count + _delimiter.DelimiterBytes.Length];
            Array.Copy(payload, offset, buffer, 0, count);
            Array.Copy(_delimiter.DelimiterBytes, 0, buffer, count, _delimiter.DelimiterBytes.Length);

            frameBuffer = buffer;
            frameBufferOffset = 0;
            frameBufferLength = buffer.Length;
        }
    }

    public sealed class LineBasedFrameDecoder : IFrameDecoder
    {
        private readonly LineDelimiter _delimiter;

        public LineBasedFrameDecoder()
            : this(LineDelimiter.CRLF)
        {
        }

        public LineBasedFrameDecoder(LineDelimiter delimiter)
        {
            if (delimiter == null)
                throw new ArgumentNullException("delimiter");
            _delimiter = delimiter;
        }

        public LineDelimiter LineDelimiter { get { return _delimiter; } }

        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count < _delimiter.DelimiterBytes.Length)
                return false;

            var delimiter = _delimiter.DelimiterBytes;
            bool matched = false;
            for (int i = 0; i < count; i++)
            {
                for (int j = 0; j < delimiter.Length; j++)
                {
                    if (i + j < count && buffer[offset + i + j] == delimiter[j])
                    {
                        matched = true;
                    }
                    else
                    {
                        matched = false;
                        break;
                    }
                }

                if (matched)
                {
                    frameLength = i + delimiter.Length;
                    payload = buffer;
                    payloadOffset = offset;
                    payloadCount = i;
                    return true;
                }
            }

            return false;
        }
    }
}
