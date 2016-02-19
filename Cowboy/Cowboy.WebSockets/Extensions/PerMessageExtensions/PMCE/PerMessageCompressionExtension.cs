using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.WebSockets.Extensions
{
    public sealed class PerMessageCompressionExtension : IWebSocketExtension
    {
        // Any extension-token used MUST be a registered token (see
        // Section 11.4).  The parameters supplied with any given extension MUST
        // be defined for that extension.  Note that the client is only offering
        // to use any advertised extensions and MUST NOT use them unless the
        // server indicates that it wishes to use the extension.
        public static readonly string RegisteredToken = @"permessage-deflate";

        private readonly DeflateCompression _deflater;
        private SortedList<int, AgreedExtensionParameter> _agreedParameters;

        public PerMessageCompressionExtension()
        {
            _deflater = new DeflateCompression();
        }

        public PerMessageCompressionExtension(SortedList<int, AgreedExtensionParameter> agreedParameters)
            : this()
        {
            _agreedParameters = agreedParameters;
        }

        public string Name { get { return RegisteredToken; } }

        // PMCEs use the RSV1 bit of the WebSocket frame header to indicate whether a
        // message is compressed or not so that an endpoint can choose not to
        // compress messages with incompressible contents.
        public bool Rsv1BitOccupied { get { return true; } }
        public bool Rsv2BitOccupied { get { return false; } }
        public bool Rsv3BitOccupied { get { return false; } }

        public string GetAgreedOffer()
        {
            var sb = new StringBuilder();

            sb.Append(this.Name);

            if (_agreedParameters != null && _agreedParameters.Any())
            {
                foreach (var parameter in _agreedParameters.Values)
                {
                    sb.Append("; ");
                    sb.Append(parameter.ToString());
                }
            }

            return sb.ToString();
        }

        public byte[] BuildExtensionData(byte[] payload, int offset, int count)
        {
            // Payload data:  (x+y) bytes
            // 
            //    The "Payload data" is defined as "Extension data" concatenated
            //    with "Application data".
            // 
            // Extension data:  x bytes
            // 
            //    The "Extension data" is 0 bytes unless an extension has been
            //    negotiated.  Any extension MUST specify the length of the
            //    "Extension data", or how that length may be calculated, and how
            //    the extension use MUST be negotiated during the opening handshake.
            //    If present, the "Extension data" is included in the total payload
            //    length.
            // 
            // Application data:  y bytes
            // 
            //    Arbitrary "Application data", taking up the remainder of the frame
            //    after any "Extension data".  The length of the "Application data"
            //    is equal to the payload length minus the length of the "Extension
            //    data".
            return null; // PMCE doesn't have an extension data definition.
        }

        public byte[] ProcessIncomingMessagePayload(byte[] payload, int offset, int count)
        {
            return _deflater.Decompress(payload, offset, count);
        }

        public byte[] ProcessOutgoingMessagePayload(byte[] payload, int offset, int count)
        {
            return _deflater.Compress(payload, offset, count);
        }
    }
}
