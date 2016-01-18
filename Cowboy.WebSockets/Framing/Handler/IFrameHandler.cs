namespace Cowboy.WebSockets
{
    public interface IFrameHandler
    {
        byte[] EncodeFrame(byte[] payload, int offset, int count, OpCode opCode, bool isFinal, bool isMasked);
        bool TryDecodeFrame(byte[] buffer, int count, bool isMasked, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount);
    }
}
