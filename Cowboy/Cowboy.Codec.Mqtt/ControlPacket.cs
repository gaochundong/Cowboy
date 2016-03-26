using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Codec.Mqtt
{
    public abstract class ControlPacket
    {
        public abstract ControlPacketType ControlPacketType { get; }

        public byte[] BuildPacketBytes()
        {
            var payloadBytes = GetPayloadBytes();
            var variableHeaderBytes = GetVariableHeaderBytes();
            var remainingLengthBytes = GetRemainingLengthBytes(
                variableHeaderBytes != null ? variableHeaderBytes.Count : 0,
                payloadBytes != null ? payloadBytes.Count : 0);
            byte fixedHeaderByte = GetFixedHeaderByte();

            var packet = new List<byte>();
            packet.Add(fixedHeaderByte);
            packet.AddRange(remainingLengthBytes);
            if (variableHeaderBytes != null)
                packet.AddRange(variableHeaderBytes);
            if (payloadBytes != null)
                packet.AddRange(payloadBytes);

            return packet.ToArray();
        }

        protected virtual byte GetFixedHeaderByte()
        {
            return (byte)((byte)this.ControlPacketType << 4);
        }

        private List<byte> GetRemainingLengthBytes(int variableHeaderLength, int payloadLength)
        {
            var remainingLengthBytes = new List<byte>();
            int totalLength = variableHeaderLength + payloadLength;

            do
            {
                int encodedByte = totalLength % 128;
                totalLength = totalLength / 128;
                if (totalLength > 0)
                {
                    encodedByte = encodedByte | 0x80;
                }
                remainingLengthBytes.Add((byte)encodedByte);
            }
            while (totalLength > 0);

            return remainingLengthBytes;
        }

        protected abstract List<byte> GetVariableHeaderBytes();

        protected abstract List<byte> GetPayloadBytes();
    }
}
