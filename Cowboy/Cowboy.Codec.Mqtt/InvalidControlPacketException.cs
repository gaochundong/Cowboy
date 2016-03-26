using System;

namespace Cowboy.Codec.Mqtt
{
    [Serializable]
    public class InvalidControlPacketException : Exception
    {
        public InvalidControlPacketException(string message)
            : base(message)
        {
        }

        public InvalidControlPacketException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
