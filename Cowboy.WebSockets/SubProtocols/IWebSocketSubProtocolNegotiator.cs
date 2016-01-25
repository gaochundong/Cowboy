using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets.SubProtocols
{
    public interface IWebSocketSubProtocolNegotiator
    {
        bool NegotiateAsClient(string protocolName, string protocolVersion, string protocolParameter, out string invalidParameter, out IWebSocketSubProtocol negotiatedSubProtocol);
        bool NegotiateAsServer(string protocolName, string protocolVersion, string protocolParameter, out string invalidParameter, out IWebSocketSubProtocol negotiatedSubProtocol);
    }
}
