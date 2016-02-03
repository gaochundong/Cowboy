using System;
using System.Collections.Generic;
using System.Linq;

namespace Cowboy.WebSockets.Extensions
{
    public sealed class PerMessageCompressionExtensionParameters
    {
        public const string ServerNoContextTakeOverParameterName = @"server_no_context_takeover";
        public const string ClientNoContextTakeOverParameterName = @"client_no_context_takeover";
        public const string ServerMaxWindowBitsParameterName = @"server_max_window_bits";
        public const string ClientMaxWindowBitsParameterName = @"client_max_window_bits";

        public static readonly SingleParameter ServerNoContextTakeOver = new SingleParameter(ServerNoContextTakeOverParameterName);
        public static readonly SingleParameter ClientNoContextTakeOver = new SingleParameter(ClientNoContextTakeOverParameterName);
        public static readonly AbsentableValueParameter<byte> ServerMaxWindowBits = new AbsentableValueParameter<byte>(ServerMaxWindowBitsParameterName, ValidateServerMaxWindowBitsParameterValue, 15);
        public static readonly AbsentableValueParameter<byte> ClientMaxWindowBits = new AbsentableValueParameter<byte>(ClientMaxWindowBitsParameterName, ValidateClientMaxWindowBitsParameterValue, 15);

        public static readonly IEnumerable<ExtensionParameter> AllAvailableParameters = new List<ExtensionParameter>()
        {
            ServerNoContextTakeOver,
            ClientNoContextTakeOver,
            ServerMaxWindowBits,
            ClientMaxWindowBits,
        };

        public static readonly IEnumerable<string> AllAvailableParameterNames = AllAvailableParameters.Select(p => p.Name);

        private static bool ValidateServerMaxWindowBitsParameterValue(string @value)
        {
            // A client MAY include the "server_max_window_bits" extension parameter
            // in an extension negotiation offer.  This parameter has a decimal
            // integer value without leading zeroes between 8 to 15, inclusive,
            // indicating the base-2 logarithm of the LZ77 sliding window size, and
            // MUST conform to the ABNF below.
            // server-max-window-bits = 1*DIGIT

            if (string.IsNullOrWhiteSpace(@value))
                return false;

            int paramValue = -1;
            if (int.TryParse(@value, out paramValue))
            {
                if (8 <= paramValue && paramValue <= 15)
                    return true;
            }

            return false;
        }

        private static bool ValidateClientMaxWindowBitsParameterValue(string @value)
        {
            // A client MAY include the "client_max_window_bits" extension parameter
            // in an extension negotiation offer.  This parameter has no value or a
            // decimal integer value without leading zeroes between 8 to 15
            // inclusive indicating the base-2 logarithm of the LZ77 sliding window
            // size.  If a value is specified for this parameter, the value MUST
            // conform to the ABNF below.
            // client-max-window-bits = 1*DIGIT

            if (string.IsNullOrWhiteSpace(@value))
                return false;

            int paramValue = -1;
            if (int.TryParse(@value, out paramValue))
            {
                if (8 <= paramValue && paramValue <= 15)
                    return true;
            }

            return false;
        }

        public static bool ValidateParameter(string parameter)
        {
            if (string.IsNullOrWhiteSpace(parameter))
                return false;

            var keyValuePair = parameter.TrimStart().TrimEnd().Split('=');
            var inputParameterName = keyValuePair[0].TrimStart().TrimEnd();
            ExtensionParameter matchedParameter = null;

            foreach (var @param in AllAvailableParameters)
            {
                if (string.Compare(inputParameterName, @param.Name, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    matchedParameter = @param;
                    break;
                }
            }

            if (matchedParameter == null)
                return false;

            switch (matchedParameter.ParameterType)
            {
                case ExtensionParameterType.Single:
                    {
                        if (keyValuePair.Length == 1)
                            return true;
                    }
                    break;
                case ExtensionParameterType.Valuable:
                    {
                        if (keyValuePair.Length != 2)
                            return false;

                        var inputParameterValue = keyValuePair[1].TrimStart().TrimEnd();
                        if (((ValuableParameter<byte>)matchedParameter).ValueValidator.Invoke(inputParameterValue))
                            return true;
                    }
                    break;
                case ExtensionParameterType.Single | ExtensionParameterType.Valuable:
                    {
                        if (keyValuePair.Length == 1)
                            return true;

                        if (keyValuePair.Length > 2)
                            return false;

                        var inputParameterValue = keyValuePair[1].TrimStart().TrimEnd();
                        if (((AbsentableValueParameter<byte>)matchedParameter).ValueValidator.Invoke(inputParameterValue))
                            return true;
                    }
                    break;
                default:
                    throw new NotSupportedException("Invalid parameter type.");
            }

            return false;
        }

        public static AgreedExtensionParameter ResolveParameter(string parameter)
        {
            if (!ValidateParameter(parameter))
                return null;

            var keyValuePair = parameter.TrimStart().TrimEnd().Split('=');
            var inputParameterName = keyValuePair[0].TrimStart().TrimEnd();
            ExtensionParameter matchedParameter = null;

            foreach (var @param in AllAvailableParameters)
            {
                if (string.Compare(inputParameterName, @param.Name, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    matchedParameter = @param;
                    break;
                }
            }

            switch (matchedParameter.Name)
            {
                case ServerNoContextTakeOverParameterName:
                case ClientNoContextTakeOverParameterName:
                    {
                        return new AgreedSingleParameter(matchedParameter.Name);
                    }
                case ServerMaxWindowBitsParameterName:
                case ClientMaxWindowBitsParameterName:
                    {
                        if (keyValuePair.Length == 1)
                            return new AgreedValuableParameter<byte>(matchedParameter.Name, ((AbsentableValueParameter<byte>)matchedParameter).DefaultValue);

                        var inputParameterValue = keyValuePair[1].TrimStart().TrimEnd();
                        return new AgreedValuableParameter<byte>(matchedParameter.Name, byte.Parse(inputParameterValue));
                    }
                default:
                    throw new NotSupportedException("Invalid parameter type.");
            }
        }
    }
}
