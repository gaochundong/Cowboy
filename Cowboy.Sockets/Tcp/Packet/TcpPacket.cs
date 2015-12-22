using System;

namespace Cowboy.Sockets
{
    public sealed class TcpPacket
    {
        private TcpPacketHeader _header;
        private byte[] _payload;
        private int _payloadRemaining = 0;

        private TcpPacket() { }

        public int HeaderSize { get { return TcpPacketHeader.HEADER_SIZE; } }
        public TcpPacketHeader Header { get { return _header; } }

        public int PayloadSize { get; private set; }
        public byte[] Payload { get { return _payload; } }

        public int Length { get { return HeaderSize + PayloadSize; } }
        public bool HasRemaining { get { return _payloadRemaining > 0; } }

        private TcpPacket PaddingByPayload(byte[] payload, int payloadSize)
        {
            this.PayloadSize = payloadSize;
            this._payload = payload;

            _header = new TcpPacketHeader()
            {
                PayloadSize = this.PayloadSize,
            };

            return this;
        }

        private TcpPacket PaddingByPacket(byte[] packet, int packetSize)
        {
            _header = TcpPacketHeader.ReadHeader(packet);
            this.PayloadSize = _header.PayloadSize;

            _payload = new byte[this.PayloadSize];
            Array.Copy(packet, HeaderSize, _payload, 0, packetSize - HeaderSize);

            _payloadRemaining = this.PayloadSize - (packetSize - HeaderSize);

            return this;
        }

        public TcpPacket AppendRemainingSegment(byte[] segment, out int remainingSegmentLength)
        {
            return AppendRemainingSegment(segment, segment.Length, out remainingSegmentLength);
        }

        public TcpPacket AppendRemainingSegment(byte[] segment, int segmentLength, out int remainingSegmentLength)
        {
            remainingSegmentLength = 0;
            int appendLength = _payloadRemaining;
            if (appendLength > segmentLength)
                appendLength = segmentLength;
            remainingSegmentLength = segmentLength - appendLength;

            Array.Copy(segment, 0, _payload, PayloadSize - _payloadRemaining, appendLength);
            _payloadRemaining = _payloadRemaining - appendLength;

            return this;
        }

        public byte[] ToArray()
        {
            byte[] packet = new byte[Length];

            Array.Copy(_header.ToArray(), 0, packet, 0, HeaderSize);
            Array.Copy(_payload, 0, packet, HeaderSize, PayloadSize);

            return packet;
        }

        public override string ToString()
        {
            return string.Format("HeaderSize[{0}], PayloadSize[{1}], Length[{2}]",
                this.HeaderSize, this.PayloadSize, this.Length);
        }

        public static TcpPacket FromPayload(byte[] payload)
        {
            return FromPayload(payload, payload.Length);
        }

        public static TcpPacket FromPayload(byte[] payload, int payloadSize)
        {
            var packet = new TcpPacket();
            packet.PaddingByPayload(payload, payloadSize);
            return packet;
        }

        public static TcpPacket FromPacket(byte[] packet)
        {
            return FromPacket(packet, packet.Length);
        }

        public static TcpPacket FromPacket(byte[] packet, int packetSize)
        {
            var instance = new TcpPacket();
            instance.PaddingByPacket(packet, packetSize);
            return instance;
        }
    }
}
