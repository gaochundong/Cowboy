using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Routing;
using Cowboy.Routing.Constraints;
using Cowboy.Routing.Trie;

namespace Cowboy.Routing
{
    public class RouteResolver
    {
        private readonly ModuleCatalog catalog = new ModuleCatalog();
        private readonly ModuleBuilder moduleBuilder = new ModuleBuilder();
        private readonly RouteCache routeCache = new RouteCache();
        private readonly RouteResolverTrie trie = new RouteResolverTrie(new TrieNodeFactory(new List<IRouteSegmentConstraint>()));

        public RouteResolver()
        {
            this.BuildTrie();
        }

        public ResolveResult Resolve(Context context)
        {
            var pathDecoded = HttpUtility.UrlDecode(context.Request.Path);

            var results = this.trie.GetMatches(GetMethod(context), pathDecoded, context);

            if (!results.Any())
            {
                var allowedMethods =
                    this.trie.GetOptions(pathDecoded, context).ToArray();

                if (IsOptionsRequest(context))
                {
                    return BuildOptionsResult(allowedMethods, context);
                }

                return IsMethodNotAllowed(allowedMethods) ?
                    BuildMethodNotAllowedResult(context, allowedMethods) :
                    GetNotFoundResult(context);
            }

            // Sort in descending order
            Array.Sort(results, (m1, m2) => -m1.CompareTo(m2));

            for (var index = 0; index < results.Length; index++)
            {
                var matchResult = results[index];
                if (matchResult.Condition == null || matchResult.Condition.Invoke(context))
                {
                    return this.BuildResult(context, matchResult);
                }
            }

            return GetNotFoundResult(context);
        }

        private static ResolveResult BuildMethodNotAllowedResult(Context context, IEnumerable<string> allowedMethods)
        {
            var route =
                new MethodNotAllowedRoute(context.Request.Path, context.Request.Method, allowedMethods);

            return new ResolveResult(route, new DynamicDictionary(), null);
        }

        private static bool IsMethodNotAllowed(IEnumerable<string> allowedMethods)
        {
            return allowedMethods.Any();// && !StaticConfiguration.DisableMethodNotAllowedResponses;
        }

        private static bool IsOptionsRequest(Context context)
        {
            return context.Request.Method.Equals("OPTIONS", StringComparison.Ordinal);
        }

        private void BuildTrie()
        {
            this.trie.BuildTrie(this.routeCache);
        }

        private static ResolveResult BuildOptionsResult(IEnumerable<string> allowedMethods, Context context)
        {
            var path =
                context.Request.Path;

            //var optionsResult =
            //    new OptionsRoute(path, allowedMethods);

            //return new ResolveResult(
            //    optionsResult,
            //    new DynamicDictionary(),
            //    null,
            //    null,
            //    null);
            return null;
        }

        private ResolveResult BuildResult(Context context, MatchResult result)
        {
            //var associatedModule = this.GetModuleFromMatchResult(context, result);

            //context.NegotiationContext.SetModule(associatedModule);

            //var route = associatedModule.Routes.ElementAt(result.RouteIndex);
            //var parameters = DynamicDictionary.Create(result.Parameters);

            //return new ResolveResult
            //{
            //    Route = route,
            //    Parameters = parameters,
            //    OnError = associatedModule.OnError
            //};
            return null;
        }

        //private IModule GetModuleFromMatchResult(Context context, MatchResult result)
        //{
            //var module =
            //    this.catalog.GetModule(result.ModuleType, context);

            //return this.moduleBuilder.BuildModule(module, context);
        //    return null;
        //}

        private static ResolveResult GetNotFoundResult(Context context)
        {
            return new ResolveResult
            {
                Route = new NotFoundRoute(context.Request.Method, context.Request.Path),
                Parameters = DynamicDictionary.Empty,
                OnError = null
            };
        }

        private static string GetMethod(Context context)
        {
            var requestedMethod = context.Request.Method;

            //if (!StaticConfiguration.EnableHeadRouting)
            //{
            //    return requestedMethod.Equals("HEAD", StringComparison.Ordinal) ?
            //        "GET" :
            //        requestedMethod;
            //}

            return requestedMethod;
        }
    }
}
