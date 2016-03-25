using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Codec.Mqtt
{
    public class MqttPacketBuilder
    {
        private static readonly byte[] EmptyArray = new byte[0];
        private static readonly Random _rng = new Random(DateTime.UtcNow.Millisecond);
        private static readonly int MaskingKeyLength = 4;

        public MqttPacketBuilder()
        {
        }

        public byte[] EncodePacket(CONNECT packet)
        {
            byte fixedHeaderByte = (byte)((byte)packet.ControlPacketType << 4);
            List<byte> remainingLengthBytes = GetRemainingLengthBytes();

            return null;
        }

        private List<byte> GetRemainingLengthBytes()
        {
            // The Remaining Length is the number of bytes remaining within the current packet, 
            // including data in the variable header and the payload. 
            // The Remaining Length does not include the bytes used to encode the Remaining Length.

            // The Remaining Length is encoded using a variable length encoding scheme which uses 
            // a single byte for values up to 127. Larger values are handled as follows. 
            // The least significant seven bits of each byte encode the data, and the most 
            // significant bit is used to indicate that there are following bytes in the representation. 
            // Thus each byte encodes 128 values and a "continuation bit". 
            // The maximum number of bytes in the Remaining Length field is four.

            var remainingLengthBytes = new List<byte>();
            int totalPayloadLength = 33333; // VariableHeader.Length + Payload.Length

            do
            {
                int encodedByte = totalPayloadLength % 128;
                totalPayloadLength = totalPayloadLength / 128;
                if (totalPayloadLength > 0)
                {
                    encodedByte = encodedByte | 0x80;
                }
                remainingLengthBytes.Add((byte)encodedByte);
            }
            while (totalPayloadLength > 0);

            return remainingLengthBytes;
        }

        private byte[] Encode(ControlPacketType opCode, byte[] payload, int offset, int count, bool isMasked = true, bool isFin = true)
        {
            byte[] fragment;

            if (count < 126)
            {
                fragment = new byte[2 + (isMasked ? MaskingKeyLength : 0) + count];
                fragment[1] = (byte)count;
            }
            else if (count < 65536)
            {
                fragment = new byte[2 + 2 + (isMasked ? MaskingKeyLength : 0) + count];
                fragment[1] = (byte)126;
                fragment[2] = (byte)(count / 256);
                fragment[3] = (byte)(count % 256);
            }
            else
            {
                fragment = new byte[2 + 8 + (isMasked ? MaskingKeyLength : 0) + count];
                fragment[1] = (byte)127;

                int left = count;
                for (int i = 9; i > 1; i--)
                {
                    fragment[i] = (byte)(left % 256);
                    left = left / 256;

                    if (left == 0)
                        break;
                }
            }

            // FIN:  1 bit
            // Indicates that this is the final fragment in a message.  The first
            // fragment MAY also be the final fragment.
            if (isFin)
                fragment[0] = 0x80;

            // Opcode:  4 bits
            // Defines the interpretation of the "Payload data".  If an unknown
            // opcode is received, the receiving endpoint MUST _Fail the
            // WebSocket Connection_.  The following values are defined.
            fragment[0] = (byte)(fragment[0] | (byte)opCode);

            // Mask:  1 bit
            // Defines whether the "Payload data" is masked.  If set to 1, a
            // masking key is present in masking-key, and this is used to unmask
            // the "Payload data" as per Section 5.3.  All packets sent from
            // client to server have this bit set to 1.
            if (isMasked)
                fragment[1] = (byte)(fragment[1] | 0x80);

            // Masking-key:  0 or 4 bytes
            // All packets sent from the client to the server are masked by a
            // 32-bit value that is contained within the packet.
            // The masking key is a 32-bit value chosen at random by the client.
            // When preparing a masked packet, the client MUST pick a fresh masking
            // key from the set of allowed 32-bit values.  The masking key needs to
            // be unpredictable; thus, the masking key MUST be derived from a strong
            // source of entropy, and the masking key for a given packet MUST NOT
            // make it simple for a server/proxy to predict the masking key for a
            // subsequent packet.  The unpredictability of the masking key is
            // essential to prevent authors of malicious applications from selecting
            // the bytes that appear on the wire.  RFC 4086 [RFC4086] discusses what
            // entails a suitable source of entropy for security-sensitive applications.
            if (isMasked)
            {
                int maskingKeyIndex = fragment.Length - (MaskingKeyLength + count);
                for (var i = maskingKeyIndex; i < maskingKeyIndex + MaskingKeyLength; i++)
                {
                    fragment[i] = (byte)_rng.Next(0, 255);
                }

                if (count > 0)
                {
                    int payloadIndex = fragment.Length - count;
                    for (var i = 0; i < count; i++)
                    {
                        fragment[payloadIndex + i] = (byte)(payload[offset + i] ^ fragment[maskingKeyIndex + i % MaskingKeyLength]);
                    }
                }
            }
            else
            {
                if (count > 0)
                {
                    int payloadIndex = fragment.Length - count;
                    Array.Copy(payload, offset, fragment, payloadIndex, count);
                }
            }

            return fragment;
        }
    }
}
