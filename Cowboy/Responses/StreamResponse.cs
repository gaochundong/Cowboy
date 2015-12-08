using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses
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
