using Cowboy.Sockets;

namespace Cowboy.Codec.Mqtt
{
    public sealed class MqttPacketBuilder : IFrameBuilder
    {
        public MqttPacketBuilder()
        {
        }

        public byte[] EncodePacket(CONNECT packet)
        {
            var bytes = packet.BuildPacketBytes();
            return null;
        }

        public void EncodeFrame(byte[] payload, int offset, int count, out byte[] frameBuffer, out int frameBufferOffset, out int frameBufferLength)
        {
            frameBuffer = payload;
            frameBufferOffset = offset;
            frameBufferLength = count;
        }

        public bool TryDecodeFrame(byte[] buffer, int offset, int count, out int frameLength, out byte[] payload, out int payloadOffset, out int payloadCount)
        {
            frameLength = 0;
            payload = null;
            payloadOffset = 0;
            payloadCount = 0;

            if (count <= 0)
                return false;

            frameLength = count;
            payload = buffer;
            payloadOffset = offset;
            payloadCount = count;
            return true;
        }
    }
}
