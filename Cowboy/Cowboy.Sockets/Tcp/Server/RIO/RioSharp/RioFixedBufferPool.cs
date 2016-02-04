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
using System.Collections.Concurrent;
using System.Runtime.InteropServices;

namespace Cowboy.Sockets.Experimental
{
    public class RioFixedBufferPool : IDisposable
    {
        ConcurrentQueue<RioBufferSegment> _availableSegments = new ConcurrentQueue<RioBufferSegment>();
        IntPtr _segmentpointer;

        internal IntPtr BufferPointer { get; set; }
        internal int TotalLength { get; set; }
        internal RioBufferSegment[] AllSegments;

        public RioFixedBufferPool(int segmentCount, int segmentLength)
        {
            AllSegments = new RioBufferSegment[segmentCount];
            TotalLength = segmentCount * segmentLength;
            BufferPointer = Marshal.AllocHGlobal(TotalLength);
            _segmentpointer = Marshal.AllocHGlobal(Marshal.SizeOf<RIO_BUFSEGMENT>() * segmentCount);

            for (int i = 0; i < segmentCount; i++)
            {
                var b = new RioBufferSegment(this, BufferPointer, _segmentpointer, i, segmentLength);
                AllSegments[i] = b;
                _availableSegments.Enqueue(b);
            }
        }

        public void SetBufferId(IntPtr id)
        {
            for (int i = 0; i < AllSegments.Length; i++)
                AllSegments[i].SetBufferId(id);
        }

        public bool TryGetBuffer(out RioBufferSegment buf)
        {
            return _availableSegments.TryDequeue(out buf);
        }

        public RioBufferSegment GetBuffer()
        {
            RioBufferSegment buf;
            do
            {
                if (_availableSegments.TryDequeue(out buf))
                    return buf;
            } while (true);
        }

        public RioBufferSegment GetBuffer(int requestedBufferSize)
        {
            RioBufferSegment buf;
            do
            {
                if (_availableSegments.TryDequeue(out buf))
                    return buf;
            } while (true);
        }

        public void ReleaseBuffer(RioBufferSegment bufferIndex)
        {
            _availableSegments.Enqueue(bufferIndex);
        }

        public void Dispose()
        {
            Marshal.FreeHGlobal(BufferPointer);
            Marshal.FreeHGlobal(_segmentpointer);
        }
    }
}
