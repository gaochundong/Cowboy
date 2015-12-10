using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using Cowboy.Responses;
using Cowboy.Responses.Serialization;

namespace Cowboy
{
    public class ResponseFormatter
    {
        private readonly Context context;
        private readonly IEnumerable<ISerializer> serializers;
        private static ISerializer jsonSerializer;
        private static ISerializer xmlSerializer;

        public ResponseFormatter(Context context, IEnumerable<ISerializer> serializers)
        {
            this.context = context;
            this.serializers = serializers.ToArray();
        }

        public IEnumerable<ISerializer> Serializers
        {
            get
            {
                return this.serializers;
            }
        }

        public Context Context
        {
            get { return this.context; }
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
            return new GenericFileResponse(applicationRelativeFilePath, contentType);
        }

        public Response AsFile(string applicationRelativeFilePath)
        {
            return new GenericFileResponse(applicationRelativeFilePath);
        }

        public Response AsJson<TModel>(TModel model, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var serializer = jsonSerializer ?? (jsonSerializer = this.Serializers.FirstOrDefault(s => s.CanSerialize("application/json")));

            var r = new JsonResponse<TModel>(model, serializer);
            r.StatusCode = statusCode;

            return r;
        }

        public Response AsXml<TModel>(TModel model)
        {
            var serializer = xmlSerializer ?? (xmlSerializer = this.Serializers.FirstOrDefault(s => s.CanSerialize("application/xml")));

            return new XmlResponse<TModel>(model, serializer);
        }

        public Response FromStream(Stream stream, string contentType)
        {
            return new StreamResponse(() => stream, contentType);
        }

        public Response FromStream(Func<Stream> streamDelegate, string contentType)
        {
            return new StreamResponse(streamDelegate, contentType);
        }
    }
}
