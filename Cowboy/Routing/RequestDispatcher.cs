using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Responses.Negotiation;

namespace Cowboy.Routing
{
    public class RequestDispatcher
    {
        private readonly RouteResolver routeResolver;
        private readonly IEnumerable<IResponseProcessor> responseProcessors;
        private readonly RouteInvoker routeInvoker;

        public RequestDispatcher(
            RouteResolver routeResolver,
            IEnumerable<IResponseProcessor> responseProcessors,
            RouteInvoker routeInvoker)
        {
            this.routeResolver = routeResolver;
            this.responseProcessors = responseProcessors;
            this.routeInvoker = routeInvoker;
        }

        public async Task<Response> Dispatch(Context context, CancellationToken cancellationToken)
        {
            var resolveResult = this.Resolve(context);

            context.Parameters = resolveResult.Parameters;
            context.ResolvedRoute = resolveResult.Route;

            return await this.routeInvoker.Invoke(resolveResult.Route, cancellationToken, resolveResult.Parameters, context);
        }

        private ResolveResult Resolve(Context context)
        {
            var extension = context.Request.Path.IndexOfAny(Path.GetInvalidPathChars()) >= 0 ? null : Path.GetExtension(context.Request.Path);

            var originalAcceptHeaders = context.Request.Headers.Accept;
            var originalRequestPath = context.Request.Path;

            if (!string.IsNullOrEmpty(extension))
            {
                var mappedMediaRanges = this.GetMediaRangesForExtension(extension.Substring(1)).ToArray();

                if (mappedMediaRanges.Any())
                {
                    var newMediaRanges = mappedMediaRanges.Where(x => !context.Request.Headers.Accept.Any(header => header.Equals(x)));

                    var index = context.Request.Path.LastIndexOf(extension, StringComparison.Ordinal);

                    var modifiedRequestPath = context.Request.Path.Remove(index, extension.Length);

                    var match = this.InvokeRouteResolver(context, modifiedRequestPath, newMediaRanges);

                    if (!(match.Route is NotFoundRoute))
                    {
                        return match;
                    }
                }
            }

            return this.InvokeRouteResolver(context, originalRequestPath, originalAcceptHeaders);
        }

        private IEnumerable<Tuple<string, decimal>> GetMediaRangesForExtension(string extension)
        {
            return this.responseProcessors
                .SelectMany(processor => processor.ExtensionMappings)
                .Where(mapping => mapping != null)
                .Where(mapping => mapping.Item1.Equals(extension, StringComparison.OrdinalIgnoreCase))
                .Select(mapping => new Tuple<string, decimal>(mapping.Item2, Decimal.MaxValue))
                .Distinct();
        }

        private ResolveResult InvokeRouteResolver(Context context, string path, IEnumerable<Tuple<string, decimal>> acceptHeaders)
        {
            context.Request.Headers.Accept = acceptHeaders.ToList();
            context.Request.Url.Path = path;

            return this.routeResolver.Resolve(context);
        }
    }
}
