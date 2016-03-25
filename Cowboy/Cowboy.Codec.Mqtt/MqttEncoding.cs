using System;
using System.Collections.Generic;
using System.Text;

namespace Cowboy.Codec.Mqtt
{
    public sealed class MqttEncoding
    {
        private Encoding _encoding;

        private MqttEncoding()
            : this(Encoding.UTF8)
        {
        }

        private MqttEncoding(Encoding encoding)
        {
            if (encoding == null)
                throw new ArgumentNullException("encoding");
            _encoding = encoding;
        }

        public static MqttEncoding Default = new MqttEncoding();

        public int GetByteCount(string str)
        {
            return _encoding.GetByteCount(str) + 2;
        }

        public byte[] GetBytes(string str)
        {
            var bytes = _encoding.GetBytes(str);

            var stringBytes = new List<byte>();
            stringBytes.Add((byte)(bytes.Length >> 8));
            stringBytes.Add((byte)(bytes.Length & 0xFF));
            stringBytes.AddRange(bytes);

            return stringBytes.ToArray();
        }

        public int GetStringLength(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 2)
                throw new ArgumentException("Invalid bytes length for length prefixed string.");

            return (ushort)((bytes[0] << 8) + bytes[1]);
        }

        public string GetString(byte[] bytes)
        {
            return _encoding.GetString(bytes);
        }

        public string GetString(byte[] bytes, int offset, int count)
        {
            return _encoding.GetString(bytes, offset, count);
        }
    }
}
