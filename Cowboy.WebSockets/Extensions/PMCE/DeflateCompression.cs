using System.IO;
using System.IO.Compression;

namespace Cowboy.WebSockets.Extensions
{
    public static class DeflateCompression
    {
        public static byte[] Compress(byte[] raw)
        {
            return Compress(raw, 0, raw.Length);
        }

        public static byte[] Compress(byte[] raw, int offset, int count)
        {
            using (var memory = new MemoryStream())
            {
                using (var deflate = new DeflateStream(memory, CompressionMode.Compress, true))
                {
                    deflate.Write(raw, offset, count);
                }
                return memory.ToArray();
            }
        }

        public static byte[] Decompress(byte[] raw)
        {
            return Decompress(raw, 0, raw.Length);
        }

        public static byte[] Decompress(byte[] raw, int offset, int count)
        {
            using (var input = new MemoryStream(raw, offset, count))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress))
            {
                const int size = 1024;
                byte[] buffer = new byte[size];
                using (var memory = new MemoryStream())
                {
                    int readCount = 0;
                    do
                    {
                        readCount = deflate.Read(buffer, 0, size);
                        if (readCount > 0)
                        {
                            memory.Write(buffer, 0, readCount);
                        }
                    }
                    while (readCount > 0);

                    return memory.ToArray();
                }
            }
        }
    }
}
