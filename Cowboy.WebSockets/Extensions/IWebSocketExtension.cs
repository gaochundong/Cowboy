using System.Collections.Generic;

namespace Cowboy.WebSockets.Extensions
{
    public interface IWebSocketExtension
    {
        IEnumerable<string> Parameters { get; }
    }
}
