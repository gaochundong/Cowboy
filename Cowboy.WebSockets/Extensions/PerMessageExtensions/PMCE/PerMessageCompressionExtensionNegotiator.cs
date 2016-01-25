using System;
using System.Collections.Generic;

namespace Cowboy.WebSockets.Extensions
{
    public sealed class PerMessageCompressionExtensionNegotiator : IWebSocketExtensionNegotiator
    {
        private static readonly char[] TrimableChars = new char[] { ' ', ';', '\r', '\n' };

        public bool NegotiateAsServer(string offer, out string invalidParameter, out IWebSocketExtension negotiatedExtension)
        {
            return Negotiate(offer, AgreeAsServer, out invalidParameter, out negotiatedExtension);
        }

        public bool NegotiateAsClient(string offer, out string invalidParameter, out IWebSocketExtension negotiatedExtension)
        {
            return Negotiate(offer, AgreeAsClient, out invalidParameter, out negotiatedExtension);
        }

        private bool Negotiate(string offer, Func<AgreedExtensionParameter, bool> agree, out string invalidParameter, out IWebSocketExtension negotiatedExtension)
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

            if (string.Compare(offeredExtensionName, PerMessageCompressionExtension.RegisteredToken, StringComparison.OrdinalIgnoreCase) != 0)
            {
                invalidParameter = offeredExtensionName;
                return false;
            }

            if (segements.Length == 1)
            {
                negotiatedExtension = new PerMessageCompressionExtension();
                return true;
            }

            // This set of elements MAY include multiple PMCEs with the same extension
            // name to offer the possibility to use the same algorithm with
            // different configuration parameters.
            for (int i = 1; i < segements.Length; i++)
            {
                var offeredParameter = segements[i];
                if (!PerMessageCompressionExtensionParameters.ValidateParameter(offeredParameter))
                {
                    invalidParameter = offeredParameter;
                    return false;
                }
            }

            // The order of elements is important as it specifies the client's preference.  
            // An element preceding another element has higher preference.  It is recommended
            // that a server accepts PMCEs with higher preference if the server supports them.
            var agreedSet = new SortedList<int, AgreedExtensionParameter>();

            for (int i = 1; i < segements.Length; i++)
            {
                var offeredParameter = segements[i];
                var agreeingParameter = PerMessageCompressionExtensionParameters.ResolveParameter(offeredParameter);
                if (agree(agreeingParameter))
                {
                    agreedSet.Add(i, agreeingParameter);
                }
            }

            negotiatedExtension = new PerMessageCompressionExtension(agreedSet);
            return true;
        }

        private bool AgreeAsServer(AgreedExtensionParameter parameter)
        {
            if (parameter == null)
                return false;

            switch (parameter.Name)
            {
                case PerMessageCompressionExtensionParameters.ServerNoContextTakeOverParameterName:
                case PerMessageCompressionExtensionParameters.ClientNoContextTakeOverParameterName:
                    {
                        return false;
                    }
                case PerMessageCompressionExtensionParameters.ServerMaxWindowBitsParameterName:
                case PerMessageCompressionExtensionParameters.ClientMaxWindowBitsParameterName:
                    {
                        return false;
                    }
                default:
                    throw new NotSupportedException("Invalid parameter name.");
            }
        }

        private bool AgreeAsClient(AgreedExtensionParameter parameter)
        {
            if (parameter == null)
                return false;

            switch (parameter.Name)
            {
                case PerMessageCompressionExtensionParameters.ServerNoContextTakeOverParameterName:
                case PerMessageCompressionExtensionParameters.ClientNoContextTakeOverParameterName:
                    {
                        return false;
                    }
                case PerMessageCompressionExtensionParameters.ServerMaxWindowBitsParameterName:
                case PerMessageCompressionExtensionParameters.ClientMaxWindowBitsParameterName:
                    {
                        return false;
                    }
                default:
                    throw new NotSupportedException("Invalid parameter name.");
            }
        }
    }
}
