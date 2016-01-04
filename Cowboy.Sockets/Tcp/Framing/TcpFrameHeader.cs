
namespace Cowboy.Sockets
{
    // The header has the following format:
    //  0                   1                   2                   3
    //  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    // | PayloadSize (4 bytes)                                         |
    // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    // | To be extended in the future                                  |
    // +-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+-+
    public sealed class TcpFrameHeader
    {
        public const int HEADER_SIZE = 4;
        public const int PAYLOAD_SIZE = 4;

        private byte[] _header;

        public TcpFrameHeader()
        {
        }

        public TcpFrameHeader(byte[] fromHeader)
            : this()
        {
            _header = fromHeader;

            this.PayloadSize = UnsignedInt(_header[3])
                + 256 * UnsignedInt(_header[2])
                + 65536 * UnsignedInt(_header[1])
                + 16777216 * UnsignedInt(_header[0]);
        }

        public int HeaderSize { get { return HEADER_SIZE; } }
        public int PayloadSize { get; set; }

        public byte[] ToArray()
        {
            if (_header == null)
            {
                Reset();
            }

            return _header;
        }

        public TcpFrameHeader Reset()
        {
            _header = new byte[HEADER_SIZE];

            for (int i = 0; i < 4; i++)
            {
                _header[3 - i] = (byte)(PayloadSize >> (8 * i));
            }

            return this;
        }

        public static TcpFrameHeader ReadHeader(byte[] buffer)
        {
            return ReadHeader(buffer, 0);
        }

        public static TcpFrameHeader ReadHeader(byte[] buffer, int offset)
        {
            var header = new byte[HEADER_SIZE];
            for (int i = 0; i < HEADER_SIZE; i++)
            {
                header[i] = buffer[offset + i];
            }

            return new TcpFrameHeader(header);
        }

        private static int UnsignedInt(int nb)
        {
            if (nb >= 0)
                return (nb);
            else
                return (256 + nb);
        }

        public override string ToString()
        {
            return string.Format("HeaderSize[{0}], PayloadSize[{1}]", this.HeaderSize, this.PayloadSize);
        }
    }
}
