using System;
using System.IO;

namespace Cowboy.Serialization
{
    public class UnclosableStreamWrapper : Stream, IDisposable
    {
        private readonly Stream _wrappedStream;

        public UnclosableStreamWrapper(Stream stream)
        {
            if (stream == null)
            {
                throw new ArgumentNullException("stream");
            }

            this._wrappedStream = stream;
        }

        public Stream WrappedStream
        {
            get
            {
                return this._wrappedStream;
            }
        }

        public override bool CanRead
        {
            get
            {
                return this._wrappedStream.CanRead;
            }
        }

        public override bool CanSeek
        {
            get
            {
                return this._wrappedStream.CanSeek;
            }
        }

        public override bool CanWrite
        {
            get
            {
                return this._wrappedStream.CanWrite;
            }
        }

        public override long Length
        {
            get
            {
                return this._wrappedStream.Length;
            }
        }

        public override long Position
        {
            get
            {
                return this._wrappedStream.Position;
            }

            set
            {
                this._wrappedStream.Position = value;
            }
        }

        public override bool CanTimeout
        {
            get
            {
                return this._wrappedStream.CanTimeout;
            }
        }

        public override int ReadTimeout
        {
            get
            {
                return this._wrappedStream.ReadTimeout;
            }

            set
            {
                this._wrappedStream.ReadTimeout = value;
            }
        }

        public override int WriteTimeout
        {
            get
            {
                return this._wrappedStream.WriteTimeout;
            }

            set
            {
                this._wrappedStream.WriteTimeout = value;
            }
        }

        public override void Close()
        {
        }

        public new void Dispose()
        {
        }

        public override void Flush()
        {
            this._wrappedStream.Flush();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            return this._wrappedStream.Seek(offset, origin);
        }

        public override void SetLength(long value)
        {
            this._wrappedStream.SetLength(value);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return this._wrappedStream.Read(buffer, offset, count);
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            this._wrappedStream.Write(buffer, offset, count);
        }

        public override IAsyncResult BeginRead(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this._wrappedStream.BeginRead(buffer, offset, count, callback, state);
        }

        public override IAsyncResult BeginWrite(byte[] buffer, int offset, int count, AsyncCallback callback, object state)
        {
            return this._wrappedStream.BeginWrite(buffer, offset, count, callback, state);
        }

        public override int EndRead(IAsyncResult asyncResult)
        {
            return this._wrappedStream.EndRead(asyncResult);
        }

        public override void EndWrite(IAsyncResult asyncResult)
        {
            this._wrappedStream.EndWrite(asyncResult);
        }

        public override int ReadByte()
        {
            return this._wrappedStream.ReadByte();
        }

        public override void WriteByte(byte value)
        {
            this._wrappedStream.WriteByte(value);
        }

        protected override void Dispose(bool disposing)
        {
        }
    }
}
