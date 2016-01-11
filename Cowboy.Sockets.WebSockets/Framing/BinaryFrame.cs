using System;
using Cowboy.Buffer;

namespace Cowboy.Sockets.WebSockets
{
    internal sealed class BinaryFrame : DataFrame
    {
        public BinaryFrame(ArraySegment<byte> segment, bool isMasked = true)
        {
            BufferValidator.ValidateArraySegment(segment, "segment");

            this.Data = segment.Array;
            this.Offset = segment.Offset;
            this.Count = segment.Count;
            this.IsMasked = isMasked;
        }

        public BinaryFrame(byte[] data, int offset, int count, bool isMasked = true)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            this.Data = data;
            this.Offset = offset;
            this.Count = count;
            this.IsMasked = isMasked;
        }

        public byte[] Data { get; private set; }
        public int Offset { get; private set; }
        public int Count { get; private set; }
        public bool IsMasked { get; private set; }

        public override FrameOpCode OpCode
        {
            get { return FrameOpCode.Binary; }
        }

        public byte[] ToArray()
        {
            return Encode(OpCode, Data, Offset, Count, IsMasked);
        }
    }
}
