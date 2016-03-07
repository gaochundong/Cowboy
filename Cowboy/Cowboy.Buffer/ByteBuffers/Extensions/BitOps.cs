using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.CompilerServices;

namespace Cowboy.Buffer.ByteBuffers
{
    public static class BitOps
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int RightUShift(this int value, int bits)
        {
            return unchecked((int)((uint)value >> bits));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static long RightUShift(this long value, int bits)
        {
            return unchecked((long)((ulong)value >> bits));
        }
    }
}
