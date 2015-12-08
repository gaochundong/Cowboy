using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses.Negotiation
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
