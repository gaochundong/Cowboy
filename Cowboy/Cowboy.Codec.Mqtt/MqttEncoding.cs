using System;
using System.Collections.Generic;
using System.Text;

namespace Cowboy.Codec.Mqtt
{
    public sealed class MqttEncoding
    {
        private Encoding _encoding;

        public MqttEncoding()
            : this(Encoding.UTF8)
        {
        }

        public MqttEncoding(Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException("encoding");
            _encoding = encoding;
        }

        public static MqttEncoding Default = new MqttEncoding();

        public byte[] GetBytes(string str)
        {
            SanityCheckString(str);

            var bytes = _encoding.GetBytes(str);

            var stringBytes = new List<byte>();
            stringBytes.Add((byte)(bytes.Length >> 8));
            stringBytes.Add((byte)(bytes.Length & 0xFF));
            stringBytes.AddRange(bytes);

            return stringBytes.ToArray();
        }

        public string GetString(byte[] bytes)
        {
            return _encoding.GetString(bytes);
        }

        public int GetCharCount(byte[] bytes)
        {
            if (bytes.Length < 2)
                throw new ArgumentException("Invalid bytes for length prefixed string.");

            return (ushort)((bytes[0] << 8) + bytes[1]);
        }

        public int GetByteCount(string str)
        {
            SanityCheckString(str);
            return _encoding.GetByteCount(str) + 2;
        }

        private static void SanityCheckString(string str)
        {
            foreach (var c in str)
                if (c > 0x7F)
                    throw new ArgumentException("Invalid UTF characters.");
        }
    }
}
