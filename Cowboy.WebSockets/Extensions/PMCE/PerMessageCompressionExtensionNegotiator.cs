using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public sealed class PerMessageCompressionExtensionNegotiator
    {
        private static readonly char[] TrimableChars = new char[] { ' ', ';', '\r', '\n' };

        public bool Negotiate(string offer, out string invalidParameter, out PerMessageCompressionExtension negotiatedExtension)
        {
            invalidParameter = null;
            negotiatedExtension = null;

            if (string.IsNullOrWhiteSpace(offer))
            {
                invalidParameter = offer;
                return false;
            }

            var segements = offer.Replace('\r', ' ').Replace('\n', ' ').TrimStart(TrimableChars).TrimEnd(TrimableChars).Split(';');

            var offeredExtensionName = segements[0].TrimStart(TrimableChars).TrimEnd(TrimableChars);
            if (string.IsNullOrEmpty(offeredExtensionName))
            {
                invalidParameter = offer;
                return false;
            }

            if (string.Compare(offeredExtensionName, PerMessageCompressionExtension.ExtensionName, StringComparison.OrdinalIgnoreCase) != 0)
            {
                invalidParameter = offeredExtensionName;
                return false;
            }

            if (segements.Length == 1)
            {
                negotiatedExtension = new PerMessageCompressionExtension();
                return true;
            }

            for (int i = 1; i < segements.Length; i++)
            {
                var offeredParameter = segements[i];
                if (!PerMessageCompressionExtensionParameters.ValidateParameter(offeredParameter))
                {
                    invalidParameter = offeredParameter;
                    return false;
                }
            }

            return false;
        }
    }
}
