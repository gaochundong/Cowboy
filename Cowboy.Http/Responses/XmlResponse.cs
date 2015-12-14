using System;
using System.IO;
using System.Net;
using Cowboy.Serialization;

namespace Cowboy.Http.Responses
{
    public class XmlResponse<TModel> : Response
    {
        public XmlResponse(TModel model, ISerializer serializer)
        {
            if (serializer == null)
            {
                throw new InvalidOperationException("XML Serializer not set");
            }

            this.Contents = GetXmlContents(model, serializer);
            this.ContentType = DefaultContentType;
            this.StatusCode = HttpStatusCode.OK;
        }

        private static string DefaultContentType
        {
            get { return string.Concat("application/xml", string.Empty); }
        }

        private static Action<Stream> GetXmlContents(TModel model, ISerializer serializer)
        {
            return stream => serializer.Serialize(DefaultContentType, model, stream);
        }
    }
}
