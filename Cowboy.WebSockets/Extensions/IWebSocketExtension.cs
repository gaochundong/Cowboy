using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.Extensions
{
    public interface IWebSocketExtension
    {
        IEnumerable<IWebSocketExtensionParameter> Parameters { get; set; }
    }
}
