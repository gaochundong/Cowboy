using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Responses.Negotiation
{
    public class ResponseProcessor : IResponseProcessor
    {
        public ProcessorMatch CanProcess(dynamic model, Context context)
        {
            return new ProcessorMatch
            {
                ModelResult = (model is Response) ? MatchResult.ExactMatch : MatchResult.NoMatch,
                RequestedContentTypeResult = MatchResult.DontCare
            };
        }

        public Response Process(dynamic model, Context context)
        {
            return (Response)model;
        }
    }
}
