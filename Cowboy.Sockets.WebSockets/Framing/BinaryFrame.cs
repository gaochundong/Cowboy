using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets.WebSockets
{
    public sealed class BinaryFrame : DataFrame
    {
        public BinaryFrame(ArraySegment<byte> segment)
        {
            WebSocketHelpers.ValidateArraySegment(segment, "segment");

            this.Data = segment.Array;
            this.Offset = segment.Offset;
            this.Count = segment.Count;
        }

        public BinaryFrame(byte[] data, int offset, int count)
        {
            WebSocketHelpers.ValidateBuffer(data, offset, count);

            this.Data = data;
            this.Offset = offset;
            this.Count = count;
        }

        public byte[] Data { get; private set; }
        public int Offset { get; private set; }
        public int Count { get; private set; }

        public override FrameOpCode OpCode
        {
            get { return FrameOpCode.Binary; }
        }

        public byte[] ToArray()
        {
            return Encode(OpCode, Data, Offset, Count);
        }
    }
}
