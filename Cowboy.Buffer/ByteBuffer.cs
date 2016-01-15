using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Buffer
{
    // +-------------------+------------------+------------------+
    // | discard-able bytes|  readable bytes  |  writable bytes  |
    // |                   |     (CONTENT)    |                  |
    // +-------------------+------------------+------------------+
    // |                   |                  |                  |
    // 0      <=          head      <=       tail      <=    capacity
    public class ByteBuffer
    {
        public ByteBuffer()
        {

        }

        public byte[] Array { get; set; }
        public int ArrayOffset { get; set; }

        public int ReaderIndex { get; set; }
        public int WriterIndex { get; set; }

        public int ReadableBytes { get; set; }
        public int WritableBytes { get; set; }

        public void SetWriterIndex(int index)
        {

        }

        public void WriteBytes()
        {
            var buffer = new byte[3];
            
        }
    }
}
