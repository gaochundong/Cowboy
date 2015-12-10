using System.Collections.Generic;
using System.Linq;
using Cowboy.Responses.Serialization;

namespace Cowboy.Responses.Negotiation.Processors
{
    public class JsonProcessor : IResponseProcessor
    {
        private readonly ISerializer serializer;

        public JsonProcessor(IEnumerable<ISerializer> serializers)
        {
            this.serializer = serializers.FirstOrDefault(x => x.CanSerialize("application/json"));
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
            return new JsonResponse(model, this.serializer);
        }
    }
}
