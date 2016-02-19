// The MIT License (MIT)
// 
// Copyright (c) 2015 Allan Lindqvist
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

using System;
using System.Runtime.InteropServices;

namespace Cowboy.Sockets.Experimental
{
    public sealed unsafe class RioBufferSegment : IDisposable
    {
        RioFixedBufferPool _pool;
        internal int Index;
        internal int TotalLength;
        public int CurrentContentLength => SegmentPointer->Length;
        internal bool AutoFree;
        internal byte* RawPointer;
        internal RIO_BUFSEGMENT* SegmentPointer;
        public byte* Datapointer => RawPointer;

        public int GetData(byte[] data, int offset)
        {
            var l = Math.Min((data.Length - offset), CurrentContentLength);
            Marshal.Copy(new IntPtr(RawPointer), data, offset, l);
            return l;
        }

        internal RioBufferSegment(RioFixedBufferPool pool, IntPtr bufferStartPointer, IntPtr segmentStartPointer, int index, int Length)
        {
            Index = index;
            TotalLength = Length;
            _pool = pool;
            AutoFree = true;

            var offset = index * Length;
            RawPointer = (byte*)(bufferStartPointer + offset).ToPointer();
            SegmentPointer = (RIO_BUFSEGMENT*)(segmentStartPointer + index * Marshal.SizeOf<RIO_BUFSEGMENT>()).ToPointer();

            SegmentPointer->BufferId = IntPtr.Zero;
            SegmentPointer->Offset = offset;
            SegmentPointer->Length = TotalLength;
        }

        internal void SetBufferId(IntPtr id)
        {
            SegmentPointer->BufferId = id;
        }

        public void Dispose()
        {
            AutoFree = true;
            SegmentPointer->Length = TotalLength;
            _pool.ReleaseBuffer(this);
        }
    }
}
