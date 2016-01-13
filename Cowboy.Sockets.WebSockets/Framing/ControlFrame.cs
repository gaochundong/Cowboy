using System;

namespace Cowboy.Sockets.WebSockets
{
    public abstract class ControlFrame : Frame
    {
        public byte[] ToArray()
        {
            var frame = BuildFrameArray();

            if (!ValidateFrameArray(frame))
            {
                throw new InvalidProgramException("All control frames MUST have a payload length of 125 bytes or less and MUST NOT be fragmented.");
            }

            return frame;
        }

        protected abstract byte[] BuildFrameArray();

        private static bool ValidateFrameArray(byte[] frame)
        {
            // All control frames MUST have a payload length of 125 bytes or less and MUST NOT be fragmented.
            return frame != null && frame.Length <= 125;
        }
    }
}
