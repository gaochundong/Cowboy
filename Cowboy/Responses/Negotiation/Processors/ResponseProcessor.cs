namespace Cowboy.Responses.Negotiation.Processors
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
