using System;

namespace Cowboy.Sockets
{
    public sealed class TcpFrame
    {
        private TcpFrameHeader _header;
        private byte[] _payload;

        private TcpFrame()
        {
        }

        public int HeaderSize { get { return TcpFrameHeader.HEADER_SIZE; } }
        public TcpFrameHeader Header { get { return _header; } }

        private int PayloadOffset { get; set; }
        public int PayloadSize { get; private set; }
        public byte[] Payload { get { return _payload; } }

        public int Length { get { return HeaderSize + PayloadSize; } }

        private TcpFrame BuildFromPayload(byte[] payload, int offset, int count)
        {
            this.PayloadOffset = offset;
            this.PayloadSize = count;
            this._payload = payload;

            _header = new TcpFrameHeader()
            {
                PayloadSize = this.PayloadSize,
            };

            return this;
        }

        private TcpFrame BuildFromFrame(byte[] frame, int offset, int count)
        {
            _header = TcpFrameHeader.ReadHeader(frame, offset);
            this.PayloadOffset = 0;
            this.PayloadSize = _header.PayloadSize;

            _payload = new byte[this.PayloadSize];
            Array.Copy(frame, offset + HeaderSize, _payload, 0, count - HeaderSize);

            return this;
        }

        public byte[] ToArray()
        {
            byte[] frame = new byte[Length];

            Array.Copy(_header.ToArray(), 0, frame, 0, HeaderSize);
            Array.Copy(Payload, PayloadOffset, frame, HeaderSize, PayloadSize);

            return frame;
        }

        public override string ToString()
        {
            return string.Format("HeaderSize[{0}], PayloadSize[{1}], Length[{2}]",
                this.HeaderSize, this.PayloadSize, this.Length);
        }

        public static TcpFrame FromPayload(byte[] payload)
        {
            return FromPayload(payload, 0, payload.Length);
        }

        public static TcpFrame FromPayload(byte[] payload, int offset, int count)
        {
            var frame = new TcpFrame();
            frame.BuildFromPayload(payload, offset, count);
            return frame;
        }

        public static TcpFrame FromFrame(byte[] frame)
        {
            return FromFrame(frame, 0, frame.Length);
        }

        public static TcpFrame FromFrame(byte[] frame, int offset, int count)
        {
            var instance = new TcpFrame();
            instance.BuildFromFrame(frame, offset, count);
            return instance;
        }
    }
}
