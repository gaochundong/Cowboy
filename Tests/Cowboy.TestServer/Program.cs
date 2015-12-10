using System;
using System.Collections.Generic;
using System.Diagnostics;
using Cowboy.Hosting.Self;
using Cowboy.Responses.Negotiation;
using Cowboy.Responses.Negotiation.Processors;
using Cowboy.Responses.Serialization;
using Cowboy.Routing;
using Cowboy.Routing.Constraints;
using Cowboy.Routing.Trie;

namespace Cowboy.TestServer
{
    class Program
    {
        static void Main(string[] args)
        {
            var moduleCatalog = new ModuleCatalog();
            moduleCatalog.RegisterModule(new TestModule());

            var routeSegmentExtractor = new RouteSegmentExtractor();
            var routeDescriptionProvider = new RouteDescriptionProvider();
            var routeCache = new RouteCache(routeSegmentExtractor, routeDescriptionProvider, moduleCatalog);

            var routeConstraints = new List<IRouteSegmentConstraint>();
            var trieNodeFactory = new TrieNodeFactory(routeConstraints);
            var trie = new RouteResolverTrie(trieNodeFactory);

            var serializers = new List<ISerializer>()
            {
                new JsonSerializer(),
                new XmlSerializer(),
            };
            var responseFormatterFactory = new ResponseFormatterFactory(serializers);

            var moduleBuilder = new ModuleBuilder(responseFormatterFactory);
            var routeResolver = new RouteResolver(moduleCatalog, moduleBuilder, routeCache, trie);
            var responseProcessors = new List<IResponseProcessor>()
            {
                new ResponseProcessor(),
                new JsonProcessor(serializers),
                new XmlProcessor(serializers),
            };
            var coercionConventions = new AcceptHeaderCoercionConventions(
                new List<Func<IEnumerable<Tuple<string, decimal>>, Context, IEnumerable<Tuple<string, decimal>>>>(2)
                {
                    BuiltInAcceptHeaderCoercions.BoostHtml,
                    BuiltInAcceptHeaderCoercions.CoerceBlankAcceptHeader,
                });

            var negotiator = new ResponseNegotiator(responseProcessors, coercionConventions);
            var routeInvoker = new RouteInvoker(negotiator);

            var contextFactory = new ContextFactory();
            var dispatcher = new RequestDispatcher(routeResolver, routeInvoker);
            var engine = new Engine(contextFactory, dispatcher);

            var host = new SelfHost(engine, new Uri("http://localhost:8888/"));
            host.Start();

            var navigateTo = "http://localhost:8888/";
            Console.WriteLine("Server now listening - navigating to {0}.", navigateTo);
            try
            {
                Process.Start(navigateTo);
            }
            catch (Exception)
            {
            }

            Console.ReadKey();
            Console.WriteLine("Stopped. Goodbye!");
        }
    }
}
