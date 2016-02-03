namespace Cowboy.WebSockets.SubProtocols
{
    public interface IWebSocketSubProtocolNegotiator
    {
        bool NegotiateAsClient(string protocolName, string protocolVersion, string protocolParameter, out string invalidParameter, out IWebSocketSubProtocol negotiatedSubProtocol);
        bool NegotiateAsServer(string protocolName, string protocolVersion, string protocolParameter, out string invalidParameter, out IWebSocketSubProtocol negotiatedSubProtocol);
    }
}
