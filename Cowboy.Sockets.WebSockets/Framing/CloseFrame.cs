using System.Text;

namespace Cowboy.Sockets.WebSockets
{
    internal sealed class CloseFrame : ControlFrame
    {
        public CloseFrame(bool isMasked = true)
        {
            this.IsMasked = isMasked;
        }

        public CloseFrame(WebSocketCloseStatus closeStatus, string closeStatusDescription, bool isMasked = true)
            : this(isMasked)
        {
            this.CloseStatus = closeStatus;
            this.CloseStatusDescription = closeStatusDescription;
        }

        public WebSocketCloseStatus CloseStatus { get; private set; }
        public string CloseStatusDescription { get; private set; }
        public bool IsMasked { get; private set; }

        public override FrameOpCode OpCode
        {
            get { return FrameOpCode.Close; }
        }

        public byte[] ToArray()
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
            int payloadLength = (string.IsNullOrEmpty(CloseStatusDescription) ? 0 : Encoding.UTF8.GetMaxByteCount(CloseStatusDescription.Length)) + 2;

            byte[] payload = new byte[payloadLength];

            int higherByte = (int)CloseStatus / 256;
            int lowerByte = (int)CloseStatus % 256;

            payload[0] = (byte)higherByte;
            payload[1] = (byte)lowerByte;

            if (!string.IsNullOrEmpty(CloseStatusDescription))
            {
                int count = Encoding.UTF8.GetBytes(CloseStatusDescription, 0, CloseStatusDescription.Length, payload, 2);
                return Encode(OpCode, payload, 0, 2 + count, IsMasked);
            }
            else
            {
                return Encode(OpCode, payload, 0, payload.Length, IsMasked);
            }
        }
    }
}
