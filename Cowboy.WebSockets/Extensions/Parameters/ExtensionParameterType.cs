using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    [Flags]
    public enum ExtensionParameterType : byte
    {
        Single = 0x1,
        Valuable = 0x2,
    }
}
