namespace Cowboy.WebSockets
{
    public abstract class Frame
    {
        public abstract FrameType FrameType { get; }
        public abstract OpCode OpCode { get; }

        public override string ToString()
        {
            return string.Format("OpName[{0}], OpCode[{1}]", OpCode, (byte)OpCode);
        }
    }
}
