using System.Collections.Generic;

namespace Cowboy.WebSockets.Extensions
{
    public interface IWebSocketExtension
    {
        string Name { get; }

        string GetAgreedOffer();
    }
}
