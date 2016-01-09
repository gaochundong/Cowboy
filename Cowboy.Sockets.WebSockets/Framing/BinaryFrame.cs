using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class BinaryFrame : Frame
    {
        public BinaryFrame(ArraySegment<byte> segment)
            : this(segment.Array, segment.Offset, segment.Count)
        {
        }

        public BinaryFrame(byte[] data, int offset, int count)
        {
            if (data == null)
                throw new ArgumentNullException("data");
            if (offset < 0)
                throw new ArgumentOutOfRangeException("offset");
            if (count < 0)
                throw new ArgumentOutOfRangeException("count");

            this.Data = data;
            this.Offset = offset;
            this.Count = count;
        }

        public byte[] Data { get; private set; }
        public int Offset { get; private set; }
        public int Count { get; private set; }

        public byte[] ToArray()
        {
            return Encode(OpCode.Binary, Data, Offset, Count);
        }
    }
}
