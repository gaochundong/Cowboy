using System;
using System.IO;
using System.Net;

namespace Cowboy.Http.Responses
{
    public class StreamResponse : Response
    {
        private Stream source;

        public StreamResponse(Func<Stream> source, string contentType)
        {
            this.Contents = GetResponseBodyDelegate(source);
            this.ContentType = contentType;
            this.StatusCode = HttpStatusCode.OK;
        }

        private Action<Stream> GetResponseBodyDelegate(Func<Stream> sourceDelegate)
        {
            return stream =>
            {
                using (this.source = sourceDelegate.Invoke())
                {
                    this.source.CopyTo(stream);
                }
            };
        }

        public override void Dispose()
        {
            if (this.source != null)
            {
                this.source.Dispose();
            }
        }
    }
}
