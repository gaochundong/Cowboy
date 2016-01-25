namespace Cowboy.WebSockets.Extensions
{
    public interface IWebSocketExtension
    {
        string Name { get; }

        bool Rsv1BitOccupied { get; }
        bool Rsv2BitOccupied { get; }
        bool Rsv3BitOccupied { get; }

        string GetAgreedOffer();

        byte[] BuildExtensionData(byte[] payload, int offset, int count);

        byte[] ProcessIncomingMessagePayload(byte[] payload, int offset, int count);
        byte[] ProcessOutgoingMessagePayload(byte[] payload, int offset, int count);
    }
}
