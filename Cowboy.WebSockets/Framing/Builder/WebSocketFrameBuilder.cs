using System;
using System.Text;

namespace Cowboy.WebSockets
{
    public class WebSocketFrameBuilder : IFrameBuilder
    {
        public WebSocketFrameBuilder()
        {
        }

        public byte[] EncodeFrame(PingFrame frame)
        {
            if (!string.IsNullOrEmpty(frame.Data))
            {
                var data = Encoding.UTF8.GetBytes(frame.Data);
                if (data == null || data.Length > 125)
                    throw new InvalidProgramException("All control frames must have a payload length of 125 bytes or less.");
                return Frame.Encode(frame.OpCode, data, 0, data.Length, true, frame.IsMasked);
            }
            else
            {
                return Frame.Encode(frame.OpCode, new byte[0], 0, 0, true, frame.IsMasked);
            }
        }

        public byte[] EncodeFrame(PongFrame frame)
        {
            if (!string.IsNullOrEmpty(frame.Data))
            {
                var data = Encoding.UTF8.GetBytes(frame.Data);
                if (data == null || data.Length > 125)
                    throw new InvalidProgramException("All control frames must have a payload length of 125 bytes or less.");
                return Frame.Encode(frame.OpCode, data, 0, data.Length, true, frame.IsMasked);
            }
            else
            {
                return Frame.Encode(frame.OpCode, new byte[0], 0, 0, true, frame.IsMasked);
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
                throw new InvalidProgramException("All control frames must have a payload length of 125 bytes or less.");

            byte[] payload = new byte[payloadLength];

            int higherByte = (int)frame.CloseCode / 256;
            int lowerByte = (int)frame.CloseCode % 256;

            payload[0] = (byte)higherByte;
            payload[1] = (byte)lowerByte;

            if (!string.IsNullOrEmpty(frame.CloseReason))
            {
                int count = Encoding.UTF8.GetBytes(frame.CloseReason, 0, frame.CloseReason.Length, payload, 2);
                return Frame.Encode(frame.OpCode, payload, 0, 2 + count, true, frame.IsMasked);
            }
            else
            {
                return Frame.Encode(frame.OpCode, payload, 0, payload.Length, true, frame.IsMasked);
            }
        }

        public byte[] EncodeFrame(TextFrame frame)
        {
            if (!string.IsNullOrEmpty(frame.Text))
            {
                var data = Encoding.UTF8.GetBytes(frame.Text);
                return Frame.Encode(frame.OpCode, data, 0, data.Length, true, frame.IsMasked);
            }
            else
            {
                return Frame.Encode(frame.OpCode, new byte[0], 0, 0, true, frame.IsMasked);
            }
        }

        public byte[] EncodeFrame(BinaryFrame frame)
        {
            return Frame.Encode(frame.OpCode, frame.Data, frame.Offset, frame.Count, true, frame.IsMasked);
        }







        public byte[] EncodeFrame(byte[] payload, int offset, int count, OpCode opCode, bool isFinal, bool isMasked)
        {
            return Frame.Encode(opCode, payload, offset, count, isFinal, isMasked);
        }

        public bool TryDecodeFrame(byte[] buffer, int count, bool isMasked, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            var frameHeader = Frame.DecodeHeader(buffer, count);
            if (frameHeader != null && frameHeader.Length + frameHeader.PayloadLength <= count)
            {
                if (isMasked)
                {
                    payload = Frame.DecodeMaskedPayload(buffer, frameHeader.MaskingKeyOffset, frameHeader.Length, frameHeader.PayloadLength);
                    payloadOffset = 0;
                    payloadCount = payload.Length;
                }
                else
                {
                    payload = buffer;
                    payloadOffset = frameHeader.Length;
                    payloadCount = frameHeader.PayloadLength;
                }

                frameLength = frameHeader.Length + frameHeader.PayloadLength;

                return true;
            }

            return false;
        }
    }
}
