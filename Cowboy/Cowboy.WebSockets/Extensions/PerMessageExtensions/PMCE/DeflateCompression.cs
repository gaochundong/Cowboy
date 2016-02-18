using System.Diagnostics.CodeAnalysis;
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

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static byte[] Compress(byte[] raw, int offset, int count)
        {
            using (var memory = new MemoryStream())
            {
                using (var deflate = new DeflateStream(memory, CompressionMode.Compress, leaveOpen: true))
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

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public static byte[] Decompress(byte[] raw, int offset, int count)
        {
            byte[] buffer = new byte[1024];

            using (var input = new MemoryStream(raw, offset, count))
            using (var deflate = new DeflateStream(input, CompressionMode.Decompress, leaveOpen: true))
            using (var memory = new MemoryStream())
            {
                int readCount = 0;
                do
                {
                    readCount = deflate.Read(buffer, 0, buffer.Length);
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
