using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses.Negotiation
{
    public class ResponseProcessor : IResponseProcessor
    {
        public IEnumerable<Tuple<string, MediaRange>> ExtensionMappings
        {
            get
            {
                return Enumerable.Empty<Tuple<string, MediaRange>>();
            }
        }

        public ProcessorMatch CanProcess(MediaRange requestedMediaRange, dynamic model, Context context)
        {
            return new ProcessorMatch
            {
                ModelResult = (model is Response) ? MatchResult.ExactMatch : MatchResult.NoMatch,
                RequestedContentTypeResult = MatchResult.DontCare
            };
        }

        public Response Process(MediaRange requestedMediaRange, dynamic model, Context context)
        {
            return (Response)model;
        }
    }
}
