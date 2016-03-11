using System;

namespace Cowboy.Buffer.ByteBuffers
{
    /// <summary>
    /// Exception thrown during instances where a reference count is used incorrectly
    /// </summary>
    public class IllegalReferenceCountException : InvalidOperationException
    {
        public IllegalReferenceCountException(int count)
            : base(string.Format("Illegal reference count of {0} for this object", count))
        {
        }

        public IllegalReferenceCountException(int refCnt, int increment)
            : base("refCnt: " + refCnt + ", " + (increment > 0 ? "increment: " + increment : "decrement: " + -increment))
        {
        }
    }
}
