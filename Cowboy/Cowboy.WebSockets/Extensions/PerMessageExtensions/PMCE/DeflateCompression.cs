using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.IO.Compression;
using Cowboy.WebSockets.Buffer;

namespace Cowboy.WebSockets.Extensions
{
    public class DeflateCompression
    {
        private readonly IBufferManager _bufferAllocator;

        public DeflateCompression()
            : this(16, 1024)
        {
        }

        public DeflateCompression(int initialPooledBufferCount, int bufferSize)
        {
            _bufferAllocator = new GrowingByteBufferManager(initialPooledBufferCount, bufferSize);
        }

        public byte[] Compress(byte[] raw)
        {
            return Compress(raw, 0, raw.Length);
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public byte[] Compress(byte[] raw, int offset, int count)
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

        public byte[] Decompress(byte[] raw)
        {
            return Decompress(raw, 0, raw.Length);
        }

        [SuppressMessage("Microsoft.Usage", "CA2202:Do not dispose objects multiple times")]
        public byte[] Decompress(byte[] raw, int offset, int count)
        {
            var buffer = _bufferAllocator.BorrowBuffer();

            try
            {
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
            finally
            {
                _bufferAllocator.ReturnBuffer(buffer);
            }
        }
    }
}
