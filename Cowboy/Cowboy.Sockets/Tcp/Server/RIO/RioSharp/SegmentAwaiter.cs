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
using System.Runtime.CompilerServices;
using System.Threading;

namespace Cowboy.Sockets.Experimental
{
    internal sealed class RioBufferSegmentAwaiter : INotifyCompletion, IDisposable
    {
        RioBufferSegment _currentValue;
        Action _continuation = null;
        WaitCallback _continuationWrapperDelegate;
        SpinLock _spinLock = new SpinLock();

        public RioBufferSegmentAwaiter()
        {
            _continuationWrapperDelegate = continuationWrapper;
        }

        private void continuationWrapper(object o)
        {
            var res = _continuation;
            _continuation = null;
            res();
        }

        public bool IsCompleted
        {
            get
            {
                bool taken = false;
                _spinLock.Enter(ref taken);
                var res = _currentValue != null;
                if (res)
                    _spinLock.Exit();
                return res;
            }
        }

        public void OnCompleted(Action continuation)
        {
            _continuation = continuation;
            _spinLock.Exit();
        }

        public void Set(RioBufferSegment item)
        {
            bool taken = false;
            _spinLock.Enter(ref taken);
            //if (!taken)
            //    throw new ArgumentException("fuu");

            //if (_currentValue != null)
            //    throw new ArgumentException("fuu");

            _currentValue = item;
            _spinLock.Exit();

            if (_continuation != null)
                ThreadPool.QueueUserWorkItem(_continuationWrapperDelegate, null);
        }

        public RioBufferSegment GetResult()
        {
            var res = _currentValue;
            _currentValue = null;
            return res;
        }

        public RioBufferSegmentAwaiter GetAwaiter() => this;

        public void Dispose()
        {
            _currentValue?.Dispose();
            _currentValue = null;
            if (_continuation != null)
                _continuation();
        }
    }
}
