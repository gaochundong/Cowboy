using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Cowboy.Http.Responses;
using Cowboy.Http.Serialization;

namespace Cowboy.Http
{
    public class ResponseFormatter
    {
        private readonly Context _context;
        private readonly IEnumerable<ISerializer> _serializers;
        private static ISerializer _jsonSerializer;
        private static ISerializer _xmlSerializer;

        public ResponseFormatter(Context context, IEnumerable<ISerializer> serializers)
        {
            _context = context;
            _serializers = serializers.ToArray();
        }

        public IEnumerable<ISerializer> Serializers
        {
            get
            {
                return _serializers;
            }
        }

        public Context Context
        {
            get { return _context; }
        }

        public Response FromStream(Stream stream, string contentType)
        {
            return new StreamResponse(() => stream, contentType);
        }

        public Response FromStream(Func<Stream> streamDelegate, string contentType)
        {
            return new StreamResponse(streamDelegate, contentType);
        }

        public Response AsText(string contents, string contentType)
        {
            return new TextResponse(contents, contentType);
        }

        public Response AsText(string contents)
        {
            return new TextResponse(contents);
        }

        public Response AsImage(string applicationRelativeFilePath)
        {
            return this.AsFile(applicationRelativeFilePath);
        }

        public Response AsFile(string applicationRelativeFilePath, string contentType)
        {
            return new FileResponse(applicationRelativeFilePath, contentType);
        }

        public Response AsFile(string applicationRelativeFilePath)
        {
            return new FileResponse(applicationRelativeFilePath);
        }

        public Response AsJson<TModel>(TModel model, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var serializer = _jsonSerializer ?? (_jsonSerializer = this.Serializers.FirstOrDefault(s => s.CanSerialize("application/json")));

            var r = new JsonResponse<TModel>(model, serializer);
            r.StatusCode = statusCode;

            return r;
        }

        public Response AsXml<TModel>(TModel model)
        {
            var serializer = _xmlSerializer ?? (_xmlSerializer = this.Serializers.FirstOrDefault(s => s.CanSerialize("application/xml")));

            return new XmlResponse<TModel>(model, serializer);
        }

        public Response AsRedirect(string location, RedirectResponse.RedirectType type = RedirectResponse.RedirectType.SeeOther)
        {
            return new RedirectResponse(this.Context.ToFullPath(location), type);
        }

        public Response AsHtml(string html, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            return new HtmlResponse(statusCode,
                stream =>
                {
                    var writer = new StreamWriter(stream);
                    writer.Write(html);
                    writer.Flush();
                });
        }
    }
}
