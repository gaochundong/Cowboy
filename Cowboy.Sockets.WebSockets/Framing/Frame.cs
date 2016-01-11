using System;

namespace Cowboy.Sockets.WebSockets
{
    // http://tools.ietf.org/html/rfc6455
    // This wire format for the data transfer part is described by the ABNF
    // [RFC5234] given in detail in this section.  (Note that, unlike in
    // other sections of this document, the ABNF in this section is
    // operating on groups of bits.  The length of each group of bits is
    // indicated in a comment.  When encoded on the wire, the most
    // significant bit is the leftmost in the ABNF).  A high-level overview
    // of the framing is given in the following figure.  In a case of
    // conflict between the figure below and the ABNF specified later in
    // this section, the figure is authoritative.
    //  0                   1                   2                   3
    //  0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1 2 3 4 5 6 7 8 9 0 1
    // +-+-+-+-+-------+-+-------------+-------------------------------+
    // |F|R|R|R| opcode|M| Payload len |    Extended payload length    |
    // |I|S|S|S|  (4)  |A|     (7)     |             (16/64)           |
    // |N|V|V|V|       |S|             |   (if payload len==126/127)   |
    // | |1|2|3|       |K|             |                               |
    // +-+-+-+-+-------+-+-------------+ - - - - - - - - - - - - - - - +
    // |     Extended payload length continued, if payload len == 127  |
    // + - - - - - - - - - - - - - - - +-------------------------------+
    // |                               |Masking-key, if MASK set to 1  |
    // +-------------------------------+-------------------------------+
    // | Masking-key (continued)       |          Payload Data         |
    // +-------------------------------- - - - - - - - - - - - - - - - +
    // :                     Payload Data continued ...                :
    // + - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - - +
    // |                     Payload Data continued ...                |
    // +---------------------------------------------------------------+
    public abstract class Frame
    {
        private static readonly Random _rng = new Random(DateTime.UtcNow.Millisecond);
        private static readonly int MaskingKeyLength = 4;

        public abstract FrameOpCode OpCode { get; }

        public static byte[] Encode(FrameOpCode opCode, byte[] playload, int offset, int count, bool isFinal = true)
        {
            byte[] fragment;

            // Payload length:  7 bits, 7+16 bits, or 7+64 bits.
            // The length of the "Payload data", in bytes: if 0-125, that is the
            // payload length.  If 126, the following 2 bytes interpreted as a
            // 16-bit unsigned integer are the payload length.  If 127, the
            // following 8 bytes interpreted as a 64-bit unsigned integer (the
            // most significant bit MUST be 0) are the payload length.
            if (count < 126)
            {
                fragment = new byte[2 + MaskingKeyLength + count];
                fragment[1] = (byte)count;
            }
            else if (count < 65536)
            {
                fragment = new byte[2 + 2 + MaskingKeyLength + count];
                fragment[1] = (byte)126;
                fragment[2] = (byte)(count / 256);
                fragment[3] = (byte)(count % 256);
            }
            else
            {
                fragment = new byte[2 + 8 + MaskingKeyLength + count];
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

            // Opcode:  4 bits
            // Defines the interpretation of the "Payload data".  If an unknown
            // opcode is received, the receiving endpoint MUST _Fail the
            // WebSocket Connection_.  The following values are defined.
            // *  %x0 denotes a continuation frame
            // *  %x1 denotes a text frame
            // *  %x2 denotes a binary frame
            // *  %x3-7 are reserved for further non-control frames
            // *  %x8 denotes a connection close
            // *  %x9 denotes a ping
            // *  %xA denotes a pong
            // *  %xB-F are reserved for further control frames
            if (isFinal)
                fragment[0] = (byte)((byte)opCode | 0x80);
            else
                fragment[0] = (byte)opCode;

            // Mask:  1 bit
            // Defines whether the "Payload data" is masked.  If set to 1, a
            // masking key is present in masking-key, and this is used to unmask
            // the "Payload data" as per Section 5.3.  All frames sent from
            // client to server have this bit set to 1.
            fragment[1] = (byte)(fragment[1] | 0x80);

            // Masking-key:  0 or 4 bytes
            // All frames sent from the client to the server are masked by a
            // 32-bit value that is contained within the frame.
            // The masking key is a 32-bit value chosen at random by the client.
            // When preparing a masked frame, the client MUST pick a fresh masking
            // key from the set of allowed 32-bit values.  The masking key needs to
            // be unpredictable; thus, the masking key MUST be derived from a strong
            // source of entropy, and the masking key for a given frame MUST NOT
            // make it simple for a server/proxy to predict the masking key for a
            // subsequent frame.  The unpredictability of the masking key is
            // essential to prevent authors of malicious applications from selecting
            // the bytes that appear on the wire.  RFC 4086 [RFC4086] discusses what
            // entails a suitable source of entropy for security-sensitive applications.
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
                    fragment[payloadIndex + i] = (byte)(playload[offset + i] ^ fragment[maskingKeyIndex + i % MaskingKeyLength]);
                }
            }

            return fragment;
        }

        public sealed class Header
        {
            public Header()
            {
                MaskingKey = new byte[MaskingKeyLength];
            }

            public bool IsFIN { get; set; }
            public bool IsRSV1 { get; set; }
            public bool IsRSV2 { get; set; }
            public bool IsRSV3 { get; set; }
            public FrameOpCode OpCode { get; set; }
            public bool IsMasked { get; set; }
            public int PayloadLength { get; set; }
            public byte[] MaskingKey { get; set; }
            public int Length { get; set; }
        }

        public static Header Decode(byte[] buffer, int count)
        {
            if (count < 2)
                return null;

            // parse fixed header
            var header = new Header()
            {
                IsFIN = ((buffer[0] & 0x80) == 0x80),
                IsRSV1 = ((buffer[0] & 0x40) == 0x40),
                IsRSV2 = ((buffer[0] & 0x20) == 0x20),
                IsRSV3 = ((buffer[0] & 0x10) == 0x10),
                OpCode = (FrameOpCode)(buffer[0] & 0x0f),
                IsMasked = ((buffer[1] & 0x80) == 0x80),
                PayloadLength = (buffer[1] & 0x7f),
                Length = 2,
            };

            // parse extended payload length
            if (header.PayloadLength >= 126)
            {
                if (header.PayloadLength == 126)
                    header.Length += 2;
                else
                    header.Length += 8;

                if (count < header.Length)
                    return null;

                if (header.PayloadLength == 126)
                {
                    header.PayloadLength = buffer[2] * 256 + buffer[3];
                }
                else
                {
                    int totalLength = 0;
                    int level = 1;

                    for (int i = 7; i >= 0; i--)
                    {
                        totalLength += buffer[i + 2] * level;
                        level *= 256;
                    }

                    header.PayloadLength = totalLength;
                }
            }

            // parse masking key
            if (header.IsMasked)
            {
                if (count < header.Length + MaskingKeyLength)
                    return null;

                for (int i = 0; i < MaskingKeyLength; i++)
                {
                    header.MaskingKey[i] = buffer[header.Length + i];
                }

                header.Length += MaskingKeyLength;
            }

            return header;
        }

        public override string ToString()
        {
            return string.Format("OpName[{0}], OpCode[{1}]", OpCode, (byte)OpCode);
        }
    }
}
