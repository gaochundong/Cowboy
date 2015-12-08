using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Responses.Serialization;

namespace Cowboy.Responses.Negotiation.Processors
{
    public class XmlProcessor : IResponseProcessor
    {
        private readonly ISerializer serializer;

        public XmlProcessor(IEnumerable<ISerializer> serializers)
        {
            this.serializer = serializers.FirstOrDefault(x => x.CanSerialize("application/xml"));
        }

        public ProcessorMatch CanProcess(dynamic model, Context context)
        {
            return new ProcessorMatch
            {
                ModelResult = MatchResult.DontCare,
                RequestedContentTypeResult = MatchResult.NoMatch
            };
        }

        public Response Process(dynamic model, Context context)
        {
            return CreateResponse(model, serializer);
        }

        private static Response CreateResponse(dynamic model, ISerializer serializer)
        {
            return new Response
            {
                Contents = stream => serializer.Serialize("application/xml", model, stream),
                ContentType = "application/xml",
                StatusCode = HttpStatusCode.OK
            };
        }
    }
}
