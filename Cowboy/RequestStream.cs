using System;
using System.IO;
using System.Threading.Tasks;

namespace Cowboy
{
    public class RequestStream : Stream
    {
        public static long DEFAULT_SWITCHOVER_THRESHOLD = 81920;

        private bool disableStreamSwitching;
        private readonly long thresholdLength;
        private bool isSafeToDisposeStream;
        private Stream stream;

        public RequestStream(long expectedLength, long thresholdLength, bool disableStreamSwitching)
            : this(null, expectedLength, thresholdLength, disableStreamSwitching)
        {
        }

        public RequestStream(Stream stream, long expectedLength, bool disableStreamSwitching)
            : this(stream, expectedLength, DEFAULT_SWITCHOVER_THRESHOLD, disableStreamSwitching)
        {
        }

        public RequestStream(long expectedLength, bool disableStreamSwitching)
            : this(null, expectedLength, DEFAULT_SWITCHOVER_THRESHOLD, disableStreamSwitching)
        {
        }

        public RequestStream(Stream stream, long expectedLength, long thresholdLength, bool disableStreamSwitching)
        {
            this.thresholdLength = thresholdLength;
            this.disableStreamSwitching = disableStreamSwitching;
            this.stream = stream ?? this.CreateDefaultMemoryStream(expectedLength);

            ThrowExceptionIfCtorParametersWereInvalid(this.stream, expectedLength, this.thresholdLength);

            if (!this.MoveStreamOutOfMemoryIfExpectedLengthExceedSwitchLength(expectedLength))
            {
                this.MoveStreamOutOfMemoryIfContentsLengthExceedThresholdAndSwitchingIsEnabled();
            }

            if (!this.stream.CanSeek)
            {
                var task = MoveToWritableStream();

                task.Wait();

                if (task.IsFaulted)
                {
                    throw new InvalidOperationException("Unable to copy stream", task.Exception);
                }
            }

            this.stream.Position = 0;
        }

        private Task<object> MoveToWritableStream()
        {
            var tcs = new TaskCompletionSource<object>();

            var sourceStream = this.stream;
            this.stream = new MemoryStream(StreamExtensions.BufferSize);

            sourceStream.CopyTo(this, (source, destination, ex) =>
            {
                if (ex != null)
                {
                    tcs.SetException(ex);
                }
                else
                {
                    tcs.SetResult(null);
                }
            });

            return tcs.Task;
        }

        public override bool CanRead
        {
            get { return true; }
        }

        public override bool CanSeek
        {
            get { return this.stream.CanSeek; }
        }

        public override bool CanTimeout
        {
            get { return false; }
        }

        public override bool CanWrite
        {
            get { return true; }
        }

        public override long Length
        {
            get { return this.stream.Length; }
        }

        public bool IsInMemory
        {
            get { return !(this.stream.GetType() == typeof(FileStream)); }
        }

        public override long Position
        {
            get { return this.stream.Position; }
            set
            {
                if (value < 0)
                    throw new InvalidOperationException("The position of the stream cannot be set to less than zero.");

                if (value > this.Length)
                    throw new InvalidOperationException("The position of the stream cannot exceed the length of the stream.");

                this.stream.Position = value;
            }
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.stream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this.stream.BeginWrite(buffer, offset, count, callback, state);
        }

        protected override void Dispose(bool disposing)
        {
            if (this.isSafeToDisposeStream)
            {
                ((IDisposable)this.stream).Dispose();

                var fileStream = this.stream as FileStream;
                if (fileStream != null)
                {
                    DeleteTemporaryFile(fileStream.Name);
                }
            }

            base.Dispose(disposing);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return this.stream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            this.stream.EndWrite(asyncResult);

            this.ShiftStreamToFileStreamIfNecessary();
        }

        public override void Flush()
        {
            this.stream.Flush();
        }

        public static RequestStream FromStream(Stream stream)
        {
            return FromStream(stream, 0, DEFAULT_SWITCHOVER_THRESHOLD, false);
        }

        public static RequestStream FromStream(Stream stream, long expectedLength)
        {
            return FromStream(stream, expectedLength, DEFAULT_SWITCHOVER_THRESHOLD, false);
        }

        public static RequestStream FromStream(Stream stream, long expectedLength, long thresholdLength)
        {
            return FromStream(stream, expectedLength, thresholdLength, false);
        }

        public static RequestStream FromStream(Stream stream, long expectedLength, bool disableStreamSwitching)
        {
            return FromStream(stream, expectedLength, DEFAULT_SWITCHOVER_THRESHOLD, disableStreamSwitching);
        }

        public static RequestStream FromStream(Stream stream, long expectedLength, long thresholdLength, bool disableStreamSwitching)
        {
            return new RequestStream(stream, expectedLength, thresholdLength, disableStreamSwitching);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this.stream.Read(buffer, offset, count);
        }

        public override int ReadByte()
        {
            return this.stream.ReadByte();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this.stream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this.stream.Write(buffer, offset, count);

            this.ShiftStreamToFileStreamIfNecessary();
        }

        private void ShiftStreamToFileStreamIfNecessary()
        {
            if (this.disableStreamSwitching)
            {
                return;
            }

            if (this.stream.Length >= this.thresholdLength)
            {
                var old = this.stream;
                this.MoveStreamContentsToFileStream();
                old.Close();
            }
        }

        private static FileStream CreateTemporaryFileStream()
        {
            var filePath = Path.GetTempFileName();

            return new FileStream(filePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None, 8192, true);
        }

        private Stream CreateDefaultMemoryStream(long expectedLength)
        {
            this.isSafeToDisposeStream = true;

            if (this.disableStreamSwitching || expectedLength < this.thresholdLength)
            {
                return new MemoryStream((int)expectedLength);
            }

            this.disableStreamSwitching = true;

            return CreateTemporaryFileStream();
        }

        private static void DeleteTemporaryFile(string fileName)
        {
            if (string.IsNullOrEmpty(fileName) || !File.Exists(fileName))
            {
                return;
            }

            try
            {
                File.Delete(fileName);
            }
            catch
            {
            }
        }

        private void MoveStreamOutOfMemoryIfContentsLengthExceedThresholdAndSwitchingIsEnabled()
        {
            if (!this.stream.CanSeek)
            {
                return;
            }

            try
            {
                if ((this.stream.Length > this.thresholdLength) && !this.disableStreamSwitching)
                {
                    this.MoveStreamContentsToFileStream();
                }
            }
            catch (NotSupportedException)
            {
            }
        }

        private bool MoveStreamOutOfMemoryIfExpectedLengthExceedSwitchLength(long expectedLength)
        {
            if ((expectedLength >= this.thresholdLength) && !this.disableStreamSwitching)
            {
                this.MoveStreamContentsToFileStream();
                return true;
            }
            return false;
        }

        private void MoveStreamContentsToFileStream()
        {
            var targetStream = CreateTemporaryFileStream();
            this.isSafeToDisposeStream = true;

            if (this.stream.CanSeek && this.stream.Length == 0)
            {
                this.stream.Close();
                this.stream = targetStream;
                return;
            }

            // Seek to 0 if we can, although if we can't seek, and we've already written (if the size is unknown) then
            // we are screwed anyway, and some streams that don't support seek also don't let you read the position so
            // there's no real way to check :-/
            if (this.stream.CanSeek)
            {
                this.stream.Position = 0;
            }
            this.stream.CopyTo(targetStream, 8196);
            if (this.stream.CanSeek)
            {
                this.stream.Flush();
            }

            this.stream = targetStream;

            this.disableStreamSwitching = true;
        }

        private static void ThrowExceptionIfCtorParametersWereInvalid(Stream stream, long expectedLength, long thresholdLength)
        {
            if (!stream.CanRead)
            {
                throw new InvalidOperationException("The stream must support reading.");
            }

            if (expectedLength < 0)
            {
                throw new ArgumentOutOfRangeException("expectedLength", expectedLength, "The value of the expectedLength parameter cannot be less than zero.");
            }

            if (thresholdLength < 0)
            {
                throw new ArgumentOutOfRangeException("thresholdLength", thresholdLength, "The value of the threshHoldLength parameter cannot be less than zero.");
            }
        }
    }

    internal static class StreamExtensions
    {
        internal const int BufferSize = 4096;

        public static void CopyTo(this Stream source, Stream destination, Action<Stream, Stream, Exception> onComplete)
        {
            var buffer = new byte[BufferSize];

            Action<Exception> done = e =>
            {
                if (onComplete != null)
                {
                    onComplete.Invoke(source, destination, e);
                }
            };

            AsyncCallback rc = null;

            rc = readResult =>
            {
                try
                {
                    var read = source.EndRead(readResult);

                    if (read <= 0)
                    {
                        done.Invoke(null);
                        return;
                    }

                    destination.BeginWrite(buffer, 0, read, writeResult =>
                    {
                        try
                        {
                            destination.EndWrite(writeResult);
                            source.BeginRead(buffer, 0, buffer.Length, rc, null);
                        }
                        catch (Exception ex)
                        {
                            done.Invoke(ex);
                        }

                    }, null);
                }
                catch (Exception ex)
                {
                    done.Invoke(ex);
                }
            };

            source.BeginRead(buffer, 0, buffer.Length, rc, null);
        }
    }
}
