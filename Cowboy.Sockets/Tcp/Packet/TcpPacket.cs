using System;

namespace Cowboy.Sockets
{
    public sealed class TcpPacket
    {
        private TcpPacketHeader _header;
        private byte[] _payload;

        private TcpPacket()
        {
        }

        public int HeaderSize { get { return TcpPacketHeader.HEADER_SIZE; } }
        public TcpPacketHeader Header { get { return _header; } }

        private int PayloadOffset { get; set; }
        public int PayloadSize { get; private set; }
        public byte[] Payload { get { return _payload; } }

        public int Length { get { return HeaderSize + PayloadSize; } }

        private TcpPacket BuildFromPayload(byte[] payload, int offset, int count)
        {
            this.PayloadOffset = offset;
            this.PayloadSize = count;
            this._payload = payload;

            _header = new TcpPacketHeader()
            {
                PayloadSize = this.PayloadSize,
            };

            return this;
        }

        private TcpPacket BuildFromPacket(byte[] packet, int offset, int count)
        {
            _header = TcpPacketHeader.ReadHeader(packet, offset);
            this.PayloadOffset = 0;
            this.PayloadSize = _header.PayloadSize;

            _payload = new byte[this.PayloadSize];
            Array.Copy(packet, offset + HeaderSize, _payload, 0, count - HeaderSize);

            return this;
        }

        public byte[] ToArray()
        {
            byte[] packet = new byte[Length];

            Array.Copy(_header.ToArray(), 0, packet, 0, HeaderSize);
            Array.Copy(Payload, PayloadOffset, packet, HeaderSize, PayloadSize);

            return packet;
        }

        public override string ToString()
        {
            return string.Format("HeaderSize[{0}], PayloadSize[{1}], Length[{2}]",
                this.HeaderSize, this.PayloadSize, this.Length);
        }

        public static TcpPacket FromPayload(byte[] payload)
        {
            return FromPayload(payload, 0, payload.Length);
        }

        public static TcpPacket FromPayload(byte[] payload, int offset, int count)
        {
            var packet = new TcpPacket();
            packet.BuildFromPayload(payload, offset, count);
            return packet;
        }

        public static TcpPacket FromPacket(byte[] packet)
        {
            return FromPacket(packet, 0, packet.Length);
        }

        public static TcpPacket FromPacket(byte[] packet, int offset, int count)
        {
            var instance = new TcpPacket();
            instance.BuildFromPacket(packet, offset, count);
            return instance;
        }
    }
}
