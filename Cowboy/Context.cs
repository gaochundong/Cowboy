using Cowboy.Responses.Negotiation;
using Cowboy.Routing;

namespace Cowboy
{
    public class Context
    {
        public Context()
        {
            this.NegotiationContext = new NegotiationContext();
        }

        public NegotiationContext NegotiationContext { get; set; }

        public Request Request { get; set; }

        public Response Response { get; set; }

        public Route ResolvedRoute { get; set; }

        public dynamic Parameters { get; set; }
    }
}
