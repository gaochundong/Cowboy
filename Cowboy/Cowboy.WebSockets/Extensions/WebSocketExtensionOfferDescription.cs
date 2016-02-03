using System;

namespace Cowboy.WebSockets.Extensions
{
    public sealed class WebSocketExtensionOfferDescription
    {
        public WebSocketExtensionOfferDescription(string offer)
        {
            if (string.IsNullOrWhiteSpace(offer))
                throw new ArgumentNullException("offer");
            this.ExtensionNegotiationOffer = offer;
        }

        public string ExtensionNegotiationOffer { get; private set; }
    }
}
