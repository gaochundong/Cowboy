using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;


namespace RioSharp
{
    public class RioStream : Stream
    {
        RioSocket _socket;
        RioBufferSegment _currentInputSegment;
        RioBufferSegment _currentOutputSegment;
        int _bytesReadInCurrentSegment = 0;
        int _remainingSpaceInOutputSegment = 0, _currentContentLength = 0;
        int _outputSegmentTotalLength;
        RioBufferSegmentAwaiter _incommingSegments = new RioBufferSegmentAwaiter();
        TaskCompletionSource<int> _readtcs;
        byte[] _readBuffer;
        int _readoffset;
        int _readCount;
        Action _getNewSegmentDelegate;

        public RioStream(RioSocket socket)
        {
            _socket = socket;
            _currentInputSegment = null;
            _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
            _getNewSegmentDelegate = GetNewSegment;
            socket.OnIncommingSegmentUnsafe = (sock, s) => _incommingSegments.Set(s);
            socket.BeginReceive();
        }

        public void Flush(bool moreData)
        {
            if (_remainingSpaceInOutputSegment == 0)
                _socket.CommitSend();
            else if (_remainingSpaceInOutputSegment == _outputSegmentTotalLength)
                return;
            else
            {
                unsafe
                {
                    _currentOutputSegment.SegmentPointer->Length = _outputSegmentTotalLength - _remainingSpaceInOutputSegment;
                }
                _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.NONE);

                if (moreData)
                {
                    _currentOutputSegment = _socket.SendBufferPool.GetBuffer();
                    _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
                }
                else
                {
                    _remainingSpaceInOutputSegment = 0;
                    _outputSegmentTotalLength = 0;
                }

            }
        }

        public override void Flush()
        {
            Flush(true);
        }

        private void GetNewSegment()
        {
            _currentInputSegment = _incommingSegments.GetResult();
            if (_currentInputSegment == null)
            {
                _readtcs.SetResult(0);
                return;
            }

            _bytesReadInCurrentSegment = 0;
            _currentContentLength = _currentInputSegment.CurrentContentLength;

            if (_currentContentLength == 0)
            {
                _currentInputSegment.Dispose();
                _currentInputSegment = null;
                _readtcs.SetResult(0);
                return;
            }
            else
            {
                _socket.BeginReceive();
                CompleteRead();
            }
        }

        private void CompleteRead()
        {
            var toCopy = _currentContentLength - _bytesReadInCurrentSegment;
            if (toCopy > _readCount)
                toCopy = _readCount;

            unsafe
            {
                fixed (byte* p = &_readBuffer[_readoffset])
                    Buffer.MemoryCopy(_currentInputSegment.RawPointer + _bytesReadInCurrentSegment, p, _readCount, toCopy);
            }

            _bytesReadInCurrentSegment += toCopy;

            if (_currentContentLength == _bytesReadInCurrentSegment)
            {
                _currentInputSegment.Dispose();
                _currentInputSegment = null;
            }

            _readtcs.SetResult(toCopy);
        }

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            _readtcs = new TaskCompletionSource<int>();
            _readBuffer = buffer;
            _readoffset = offset;
            _readCount = count;

            if (_currentInputSegment == null)
            {
                if (_incommingSegments.IsCompleted)
                    GetNewSegment();
                else
                    _incommingSegments.OnCompleted(_getNewSegmentDelegate);
            }
            else
                CompleteRead();

            return _readtcs.Task;
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return ReadAsync(buffer, offset, count, CancellationToken.None).Result;
        }

        public override unsafe void Write(byte[] buffer, int offset, int count)
        {
            int writtenFromBuffer = 0;
            do
            {
                if (_remainingSpaceInOutputSegment == 0)
                {
                    _currentOutputSegment.SegmentPointer->Length = _outputSegmentTotalLength;
                    _socket.SendInternal(_currentOutputSegment, RIO_SEND_FLAGS.DEFER); //| RIO_SEND_FLAGS.DONT_NOTIFY
                    while (!_socket.SendBufferPool.TryGetBuffer(out _currentOutputSegment))
                        _socket.CommitSend();
                    _outputSegmentTotalLength = _currentOutputSegment.TotalLength;
                    _remainingSpaceInOutputSegment = _outputSegmentTotalLength;
                    continue;
                }

                var toWrite = count - writtenFromBuffer;
                if (toWrite > _remainingSpaceInOutputSegment)
                    toWrite = _remainingSpaceInOutputSegment;

                fixed (byte* p = &buffer[offset])
                {
                    Buffer.MemoryCopy(p + writtenFromBuffer, _currentOutputSegment.RawPointer + (_outputSegmentTotalLength - _remainingSpaceInOutputSegment), _remainingSpaceInOutputSegment, toWrite);
                }

                writtenFromBuffer += toWrite;
                _remainingSpaceInOutputSegment -= toWrite;

            } while (writtenFromBuffer < count);
        }

        protected override void Dispose(bool disposing)
        {
            Flush(false);

            if (_currentInputSegment != null)
                _currentInputSegment.Dispose();

            _currentOutputSegment.Dispose();
            _incommingSegments.Dispose();
        }

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length { get { throw new NotImplementedException(); } }
        public override long Position { get { throw new NotImplementedException(); } set { throw new NotImplementedException(); } }
        public override long Seek(long offset, SeekOrigin origin) { return 0; }
        public override void SetLength(long value) { throw new NotImplementedException(); }

    }
}
