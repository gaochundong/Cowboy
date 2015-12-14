using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Http.Routing
{
    public class RequestDispatcher
    {
        private readonly RouteResolver routeResolver;
        private readonly RouteInvoker routeInvoker;

        public RequestDispatcher(RouteResolver routeResolver, RouteInvoker routeInvoker)
        {
            this.routeResolver = routeResolver;
            this.routeInvoker = routeInvoker;
        }

        public async Task<Response> Dispatch(Context context, CancellationToken cancellationToken)
        {
            var resolveResult = Resolve(context);

            context.Parameters = resolveResult.Parameters;
            context.ResolvedRoute = resolveResult.Route;

            return await this.routeInvoker.Invoke(resolveResult.Route, cancellationToken, resolveResult.Parameters, context);
        }

        private ResolveResult Resolve(Context context)
        {
            var originalAcceptHeaders = context.Request.Headers.Accept;
            var originalRequestPath = context.Request.Path;

            return InvokeRouteResolver(context, originalRequestPath, originalAcceptHeaders);
        }

        private ResolveResult InvokeRouteResolver(Context context, string path, IEnumerable<Tuple<string, decimal>> acceptHeaders)
        {
            context.Request.Headers.Accept = acceptHeaders.ToList();
            context.Request.Url.Path = path;

            return this.routeResolver.Resolve(context);
        }
    }
}
