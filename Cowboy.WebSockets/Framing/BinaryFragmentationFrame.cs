using System;
using Cowboy.Buffer;

namespace Cowboy.WebSockets
{
    public sealed class BinaryFragmentationFrame : Frame
    {
        private OpCode _opCode;

        public BinaryFragmentationFrame(OpCode opCode, byte[] data, int offset, int count, bool isFin = false, bool isMasked = true)
        {
            BufferValidator.ValidateBuffer(data, offset, count, "data");

            _opCode = opCode;
            this.Data = data;
            this.Offset = offset;
            this.Count = count;
            this.IsFin = isFin;
            this.IsMasked = isMasked;
        }

        public byte[] Data { get; private set; }
        public int Offset { get; private set; }
        public int Count { get; private set; }
        public bool IsFin { get; private set; }
        public bool IsMasked { get; private set; }

        public override OpCode OpCode
        {
            get { return _opCode; }
        }

        public byte[] ToArray(IFrameBuilder builder)
        {
            if (builder == null)
                throw new ArgumentNullException("builder");
            return builder.EncodeFrame(this);
        }
    }
}
