using System.Collections.Generic;

namespace Cowboy.WebSockets.Extensions
{
    public interface IWebSocketExtension
    {
        string Name { get; }

        string GetAgreedOffer();

        byte[] ProcessIncomingPayload(byte[] payload, int offset, int count);

        byte[] ProcessOutgoingPayload(byte[] payload, int offset, int count);
    }
}
