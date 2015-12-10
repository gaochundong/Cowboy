using System.Threading;
using System.Threading.Tasks;
using Cowboy.Responses.Negotiation;

namespace Cowboy.Routing
{
    public class RouteInvoker
    {
        private readonly ResponseNegotiator negotiator;

        public RouteInvoker(ResponseNegotiator negotiator)
        {
            this.negotiator = negotiator;
        }

        public async Task<Response> Invoke(Route route, CancellationToken cancellationToken, DynamicDictionary parameters, Context context)
        {
            var result = await route.Invoke(parameters, cancellationToken);
            return this.negotiator.NegotiateResponse(result, context);
        }
    }
}
