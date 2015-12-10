namespace Cowboy.Responses.Negotiation
{
    public class ProcessorMatch
    {
        public static ProcessorMatch None = new ProcessorMatch
        {
            ModelResult = MatchResult.NoMatch,
            RequestedContentTypeResult = MatchResult.NoMatch
        };

        public MatchResult RequestedContentTypeResult { get; set; }

        public MatchResult ModelResult { get; set; }
    }
}
