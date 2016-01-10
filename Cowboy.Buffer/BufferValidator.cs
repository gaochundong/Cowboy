using System;

namespace Cowboy.Buffer
{
    public class BufferValidator
    {
        public static void ValidateBuffer(byte[] buffer, int offset, int count, string bufferParameterName = null)
        {
            if (buffer == null)
            {
                throw new ArgumentNullException(!string.IsNullOrEmpty(bufferParameterName) ? bufferParameterName : "buffer");
            }

            if (offset < 0 || offset > buffer.Length)
            {
                throw new ArgumentOutOfRangeException("offset");
            }

            if (count < 0 || count > (buffer.Length - offset))
            {
                throw new ArgumentOutOfRangeException("count");
            }
        }

        public static void ValidateArraySegment<T>(ArraySegment<T> arraySegment, string parameterName)
        {
            if (arraySegment.Array == null)
            {
                throw new ArgumentNullException(parameterName + ".Array");
            }

            if (arraySegment.Offset < 0 || arraySegment.Offset > arraySegment.Array.Length)
            {
                throw new ArgumentOutOfRangeException(parameterName + ".Offset");
            }

            if (arraySegment.Count < 0 || arraySegment.Count > (arraySegment.Array.Length - arraySegment.Offset))
            {
                throw new ArgumentOutOfRangeException(parameterName + ".Count");
            }
        }
    }
}
