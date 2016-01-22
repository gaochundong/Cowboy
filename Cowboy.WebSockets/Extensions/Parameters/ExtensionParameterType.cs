using System;

namespace Cowboy.WebSockets.Extensions
{
    [Flags]
    public enum ExtensionParameterType : byte
    {
        Single = 0x1,
        Valuable = 0x2,
    }
}
