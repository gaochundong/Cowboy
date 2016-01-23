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

        private SortedList<int, AgreedExtensionParameter> _agreedParameters;

        public PerMessageCompressionExtension()
        {
        }

        public PerMessageCompressionExtension(SortedList<int, AgreedExtensionParameter> agreedParameters)
            : this()
        {
            _agreedParameters = agreedParameters;
        }

        public string Name { get { return RegisteredToken; } }

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

        public byte[] ProcessIncomingMessagePayload(byte[] payload, int offset, int count)
        {
            return DeflateCompression.Decompress(payload, offset, count);
        }

        public byte[] ProcessOutgoingMessagePayload(byte[] payload, int offset, int count)
        {
            return DeflateCompression.Compress(payload, offset, count);
        }
    }
}
