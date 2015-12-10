using System;
using System.Collections.Generic;
using System.IO;
using System.Net;

namespace Cowboy.Responses
{
    /// <summary>
    /// Represents a HTML (text/html) response
    /// </summary>
    public class HtmlResponse : Response
    {
        public HtmlResponse(HttpStatusCode statusCode = HttpStatusCode.OK, Action<Stream> contents = null, IDictionary<string, string> headers = null)
        {
            this.ContentType = "text/html";
            this.StatusCode = statusCode;

            if (contents != null)
            {
                this.Contents = contents;
            }

            if (headers != null)
            {
                this.Headers = headers;
            }
        }
    }
}
