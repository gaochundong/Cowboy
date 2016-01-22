namespace Cowboy.WebSockets.Extensions
{
    public interface IWebSocketExtensionNegotiator
    {
        bool NegotiateAsServer(string offer, out string invalidParameter, out IWebSocketExtension negotiatedExtension);
        bool NegotiateAsClient(string offer, out string invalidParameter, out IWebSocketExtension negotiatedExtension);
    }
}
