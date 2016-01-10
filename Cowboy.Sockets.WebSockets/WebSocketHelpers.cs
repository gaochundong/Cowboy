using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    internal class WebSocketHelpers
    {
        internal static void ValidateSubprotocol(string subProtocol)
        {
            if (string.IsNullOrWhiteSpace(subProtocol))
                throw new ArgumentNullException("subProtocol");

            string separators = "()<>@,;:\\\"/[]?={} ";

            char[] chars = subProtocol.ToCharArray();
            string invalidChar = null;
            int i = 0;
            while (i < chars.Length)
            {
                char ch = chars[i];
                if (ch < 0x21 || ch > 0x7e)
                {
                    invalidChar = string.Format(CultureInfo.InvariantCulture, "[{0}]", (int)ch);
                    break;
                }

                if (!char.IsLetterOrDigit(ch) && separators.IndexOf(ch) >= 0)
                {
                    invalidChar = ch.ToString();
                    break;
                }

                i++;
            }

            if (invalidChar != null)
            {
                throw new ArgumentException(string.Format("Invalid char [{0}] in sub-protocol string [{1}].", invalidChar, subProtocol), "subProtocol");
            }
        }
    }
}
