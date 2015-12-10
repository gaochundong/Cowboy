using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;

namespace Cowboy
{
    public class Response : IDisposable
    {
        public static Action<Stream> NoBody = s => { };

        private string contentType;

        public Response()
        {
            this.Contents = NoBody;
            this.ContentType = "text/html";
            this.Headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            this.StatusCode = HttpStatusCode.OK;
            //this.Cookies = new List<INancyCookie>(2);
        }

        public string ContentType
        {
            get { return Headers.ContainsKey("content-type") ? Headers["content-type"] : this.contentType; }
            set { this.contentType = value; }
        }

        public Action<Stream> Contents { get; set; }

        public IDictionary<string, string> Headers { get; set; }

        public HttpStatusCode StatusCode { get; set; }

        public string ReasonPhrase { get; set; }

        //public IList<INancyCookie> Cookies { get; private set; }

        public static implicit operator Response(HttpStatusCode statusCode)
        {
            return new Response { StatusCode = statusCode };
        }

        public static implicit operator Response(int statusCode)
        {
            return new Response { StatusCode = (HttpStatusCode)statusCode };
        }

        public static implicit operator Response(string contents)
        {
            return new Response { Contents = GetStringContents(contents) };
        }

        public static implicit operator Response(Action<Stream> streamFactory)
        {
            return new Response { Contents = streamFactory };
        }

        public static implicit operator Response(DynamicDictionaryValue value)
        {
            return new Response { Contents = GetStringContents(value) };
        }

        protected static Action<Stream> GetStringContents(string contents)
        {
            return stream =>
            {
                var writer = new StreamWriter(stream) { AutoFlush = true };
                writer.Write(contents);
            };
        }

        public virtual void Dispose()
        {
        }

        public Response WithCookie(string name, string value)
        {
            return this.WithCookie(name, value, null, null, null);
        }

        public Response WithCookie(string name, string value, DateTime? expires)
        {
            return this.WithCookie(name, value, expires, null, null);
        }

        public Response WithCookie(string name, string value, DateTime? expires, string domain, string path)
        {
            //return WithCookie(this, new NancyCookie(name, value) { Expires = expires, Domain = domain, Path = path });
            return this;
        }

        //public Response WithCookie(INancyCookie nancyCookie)
        //{
        //    this.Cookies.Add(nancyCookie);
        //    return this;
        //}

        public Response WithHeader(string header, string value)
        {
            return this.WithHeaders(new { Header = header, Value = value });
        }

        public Response WithHeaders(params object[] headers)
        {
            return this.WithHeaders(headers.Select(GetTuple).ToArray());
        }

        public Response WithHeaders(params Tuple<string, string>[] headers)
        {
            if (this.Headers == null)
            {
                this.Headers = new Dictionary<string, string>();
            }

            foreach (var keyValuePair in headers)
            {
                this.Headers[keyValuePair.Item1] = keyValuePair.Item2;
            }

            return this;
        }

        public Response WithContentType(string contentType)
        {
            this.ContentType = contentType;
            return this;
        }

        public Response WithStatusCode(HttpStatusCode statusCode)
        {
            this.StatusCode = statusCode;
            return this;
        }

        public Response WithStatusCode(int statusCode)
        {
            this.StatusCode = (HttpStatusCode)statusCode;
            return this;
        }

        private static Tuple<string, string> GetTuple(object header)
        {
            var properties = header.GetType()
                                   .GetProperties()
                                   .Where(prop => prop.CanRead && prop.PropertyType == typeof(string))
                                   .ToArray();

            var headerProperty = properties
                                    .Where(p => string.Equals(p.Name, "Header", StringComparison.OrdinalIgnoreCase))
                                    .FirstOrDefault();

            var valueProperty = properties
                                    .Where(p => string.Equals(p.Name, "Value", StringComparison.OrdinalIgnoreCase))
                                    .FirstOrDefault();

            if (headerProperty == null || valueProperty == null)
            {
                throw new ArgumentException("Unable to extract 'Header' or 'Value' properties from anonymous type.");
            }

            return Tuple.Create(
                (string)headerProperty.GetValue(header, null),
                (string)valueProperty.GetValue(header, null));
        }
    }
}
