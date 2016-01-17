namespace Cowboy.Sockets
{
    public interface IFrameHandler
    {
        byte[] EncodeFrame(byte[] payload, int offset, int count);
        bool TryDecodeFrame(byte[] buffer, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount);
    }
}
