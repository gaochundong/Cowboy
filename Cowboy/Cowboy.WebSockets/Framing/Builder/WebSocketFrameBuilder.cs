using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cowboy.WebSockets.Extensions;

namespace Cowboy.WebSockets
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
    public class WebSocketFrameBuilder : IFrameBuilder
    {
        private static readonly byte[] EmptyArray = new byte[0];
        private static readonly Random _rng = new Random(DateTime.UtcNow.Millisecond);
        private static readonly int MaskingKeyLength = 4;

        public WebSocketFrameBuilder()
        {
        }

        public SortedList<int, IWebSocketExtension> NegotiatedExtensions { get; set; }

        public byte[] EncodeFrame(PingFrame frame)
        {
            if (!string.IsNullOrEmpty(frame.Data))
            {
                var data = Encoding.UTF8.GetBytes(frame.Data);
                if (data.Length > 125)
                    throw new WebSocketException("All control frames must have a payload length of 125 bytes or less.");
                return Encode(frame.OpCode, data, 0, data.Length, isMasked: frame.IsMasked);
            }
            else
            {
                return Encode(frame.OpCode, EmptyArray, 0, 0, isMasked: frame.IsMasked);
            }
        }

        public byte[] EncodeFrame(PongFrame frame)
        {
            if (!string.IsNullOrEmpty(frame.Data))
            {
                var data = Encoding.UTF8.GetBytes(frame.Data);
                if (data.Length > 125)
                    throw new WebSocketException("All control frames must have a payload length of 125 bytes or less.");
                return Encode(frame.OpCode, data, 0, data.Length, isMasked: frame.IsMasked);
            }
            else
            {
                return Encode(frame.OpCode, EmptyArray, 0, 0, isMasked: frame.IsMasked);
            }
        }

        public byte[] EncodeFrame(CloseFrame frame)
        {
            // The Close frame MAY contain a body (the "Application data" portion of
            // the frame) that indicates a reason for closing, such as an endpoint
            // shutting down, an endpoint having received a frame too large, or an
            // endpoint having received a frame that does not conform to the format
            // expected by the endpoint.  If there is a body, the first two bytes of
            // the body MUST be a 2-byte unsigned integer (in network byte order)
            // representing a status code with value /code/ defined in Section 7.4.
            // Following the 2-byte integer, the body MAY contain UTF-8-encoded data
            // with value /reason/, the interpretation of which is not defined by
            // this specification.  This data is not necessarily human readable but
            // may be useful for debugging or passing information relevant to the
            // script that opened the connection.  As the data is not guaranteed to
            // be human readable, clients MUST NOT show it to end users.
            int payloadLength = (string.IsNullOrEmpty(frame.CloseReason) ? 0 : Encoding.UTF8.GetMaxByteCount(frame.CloseReason.Length)) + 2;
            if (payloadLength > 125)
                throw new WebSocketException("All control frames must have a payload length of 125 bytes or less.");

            byte[] payload = new byte[payloadLength];

            int higherByte = (int)frame.CloseCode / 256;
            int lowerByte = (int)frame.CloseCode % 256;

            payload[0] = (byte)higherByte;
            payload[1] = (byte)lowerByte;

            if (!string.IsNullOrEmpty(frame.CloseReason))
            {
                int count = Encoding.UTF8.GetBytes(frame.CloseReason, 0, frame.CloseReason.Length, payload, 2);
                return Encode(frame.OpCode, payload, 0, 2 + count, isMasked: frame.IsMasked);
            }
            else
            {
                return Encode(frame.OpCode, payload, 0, payload.Length, isMasked: frame.IsMasked);
            }
        }

        public byte[] EncodeFrame(TextFrame frame)
        {
            if (!string.IsNullOrEmpty(frame.Text))
            {
                var data = Encoding.UTF8.GetBytes(frame.Text);
                return Encode(frame.OpCode, data, 0, data.Length, isMasked: frame.IsMasked);
            }
            else
            {
                return Encode(frame.OpCode, EmptyArray, 0, 0, isMasked: frame.IsMasked);
            }
        }

        public byte[] EncodeFrame(BinaryFrame frame)
        {
            return Encode(frame.OpCode, frame.Data, frame.Offset, frame.Count, isMasked: frame.IsMasked);
        }

        public byte[] EncodeFrame(BinaryFragmentationFrame frame)
        {
            return Encode(frame.OpCode, frame.Data, frame.Offset, frame.Count, isMasked: frame.IsMasked, isFin: frame.IsFin);
        }

        private byte[] Encode(OpCode opCode, byte[] payload, int offset, int count, bool isMasked = true, bool isFin = true)
        {
            // Payload data:  (x+y) bytes
            // Extension data:  x bytes
            // Application data:  y bytes
            // The "Extension data" is 0 bytes unless an extension has been
            // negotiated.  Any extension MUST specify the length of the
            // "Extension data", or how that length may be calculated, and how
            // the extension use MUST be negotiated during the opening handshake.
            // If present, the "Extension data" is included in the total payload length.
            if (this.NegotiatedExtensions != null)
            {
                byte[] bakedBuffer = null;
                foreach (var extension in this.NegotiatedExtensions.Values)
                {
                    if (bakedBuffer == null)
                    {
                        bakedBuffer = extension.ProcessOutgoingMessagePayload(payload, offset, count);
                    }
                    else
                    {
                        bakedBuffer = extension.ProcessOutgoingMessagePayload(bakedBuffer, 0, bakedBuffer.Length);
                    }
                }

                payload = bakedBuffer;
                offset = 0;
                count = payload.Length;
            }

            byte[] fragment;

            // Payload length:  7 bits, 7+16 bits, or 7+64 bits.
            // The length of the "Payload data", in bytes: if 0-125, that is the
            // payload length.  If 126, the following 2 bytes interpreted as a
            // 16-bit unsigned integer are the payload length.  If 127, the
            // following 8 bytes interpreted as a 64-bit unsigned integer (the
            // most significant bit MUST be 0) are the payload length.
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

            // RSV1, RSV2, RSV3:  1 bit each
            // MUST be 0 unless an extension is negotiated that defines meanings
            // for non-zero values.  If a nonzero value is received and none of
            // the negotiated extensions defines the meaning of such a nonzero
            // value, the receiving endpoint MUST _Fail the WebSocket
            // Connection_.
            if (this.NegotiatedExtensions != null)
            {
                foreach (var extension in this.NegotiatedExtensions.Values)
                {
                    if (extension.Rsv1BitOccupied)
                        fragment[0] = (byte)(fragment[0] | 0x40);
                    if (extension.Rsv2BitOccupied)
                        fragment[0] = (byte)(fragment[0] | 0x20);
                    if (extension.Rsv3BitOccupied)
                        fragment[0] = (byte)(fragment[0] | 0x10);
                }
            }

            // Opcode:  4 bits
            // Defines the interpretation of the "Payload data".  If an unknown
            // opcode is received, the receiving endpoint MUST _Fail the
            // WebSocket Connection_.  The following values are defined.
            fragment[0] = (byte)(fragment[0] | (byte)opCode);

            // Mask:  1 bit
            // Defines whether the "Payload data" is masked.  If set to 1, a
            // masking key is present in masking-key, and this is used to unmask
            // the "Payload data" as per Section 5.3.  All frames sent from
            // client to server have this bit set to 1.
            if (isMasked)
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

        public bool TryDecodeFrameHeader(byte[] buffer, int offset, int count, out Header frameHeader)
        {
            frameHeader = DecodeFrameHeader(buffer, offset, count);
            return frameHeader != null;
        }

        private Header DecodeFrameHeader(byte[] buffer, int offset, int count)
        {
            if (count < 2)
                return null;

            // parse fixed header
            var header = new Header()
            {
                IsFIN = ((buffer[offset + 0] & 0x80) == 0x80),
                IsRSV1 = ((buffer[offset + 0] & 0x40) == 0x40),
                IsRSV2 = ((buffer[offset + 0] & 0x20) == 0x20),
                IsRSV3 = ((buffer[offset + 0] & 0x10) == 0x10),
                OpCode = (OpCode)(buffer[offset + 0] & 0x0f),
                IsMasked = ((buffer[offset + 1] & 0x80) == 0x80),
                PayloadLength = (buffer[offset + 1] & 0x7f),
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
                    header.PayloadLength = buffer[offset + 2] * 256 + buffer[offset + 3];
                }
                else
                {
                    int totalLength = 0;
                    int level = 1;

                    for (int i = 7; i >= 0; i--)
                    {
                        totalLength += buffer[offset + i + 2] * level;
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

                header.MaskingKeyOffset = header.Length;
                header.Length += MaskingKeyLength;
            }

            return header;
        }

        public void DecodePayload(byte[] buffer, int offset, Header frameHeader, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            payload = buffer;
            payloadOffset = offset + frameHeader.Length;
            payloadCount = frameHeader.PayloadLength;

            if (frameHeader.IsMasked)
            {
                payload = new byte[payloadCount];

                for (var i = 0; i < payloadCount; i++)
                {
                    payload[i] = (byte)(buffer[payloadOffset + i] ^ buffer[offset + frameHeader.MaskingKeyOffset + i % MaskingKeyLength]);
                }

                payloadOffset = 0;
                payloadCount = payload.Length;
            }

            // Payload data:  (x+y) bytes
            // Extension data:  x bytes
            // Application data:  y bytes
            // The "Extension data" is 0 bytes unless an extension has been
            // negotiated.  Any extension MUST specify the length of the
            // "Extension data", or how that length may be calculated, and how
            // the extension use MUST be negotiated during the opening handshake.
            // If present, the "Extension data" is included in the total payload length.
            if (this.NegotiatedExtensions != null)
            {
                byte[] bakedBuffer = null;
                foreach (var extension in this.NegotiatedExtensions.Reverse().Select(e => e.Value))
                {
                    if (bakedBuffer == null)
                    {
                        bakedBuffer = extension.ProcessIncomingMessagePayload(payload, payloadOffset, payloadCount);
                    }
                    else
                    {
                        bakedBuffer = extension.ProcessIncomingMessagePayload(bakedBuffer, 0, bakedBuffer.Length);
                    }
                }

                payload = bakedBuffer;
                payloadOffset = 0;
                payloadCount = payload.Length;
            }
        }
    }
}
