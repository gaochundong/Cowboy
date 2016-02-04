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
using System.Threading;

namespace Cowboy.Sockets.Experimental
{
    public unsafe class RioConnectionOrientedSocket : RioSocketBase
    {
        internal RioNativeOverlapped* _overlapped;
        internal IntPtr _adressBuffer;
        private IntPtr _eventHandle;
        private RioConnectionOrientedSocketPool _pool;

        internal RioConnectionOrientedSocket(IntPtr overlapped, IntPtr adressBuffer, RioConnectionOrientedSocketPool pool, RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool,
            uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue) :
            base(sendBufferPool, receiveBufferPool, maxOutstandingReceive, maxOutstandingSend, SendCompletionQueue, ReceiveCompletionQueue,
                ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_STREAM, PROTOCOL.IPPROTO_TCP)
        {
            _overlapped = (RioNativeOverlapped*)overlapped.ToPointer();
            _eventHandle = Kernel32.CreateEvent(IntPtr.Zero, false, false, null);
            _adressBuffer = adressBuffer;
            _pool = pool;
            unsafe
            {
                var n = (NativeOverlapped*)overlapped.ToPointer();
                n->EventHandle = _eventHandle;
            }
        }

        internal unsafe void ResetOverlapped()
        {
            _overlapped->InternalHigh = IntPtr.Zero;
            _overlapped->InternalLow = IntPtr.Zero;
            _overlapped->OffsetHigh = 0;
            _overlapped->OffsetLow = 0;
            Kernel32.ResetEvent(_overlapped->EventHandle);
        }

        public override void Dispose()
        {
            _pool.Recycle(this);
        }
    }
}

