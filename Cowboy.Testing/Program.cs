using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cowboy;
using Cowboy.Hosting.Self;
using Cowboy.Responses.Negotiation;
using Cowboy.Routing;
using Cowboy.Routing.Constraints;
using Cowboy.Routing.Trie;

namespace Cowboy.Testing
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

            var moduleBuilder = new ModuleBuilder();
            var routeResolver = new RouteResolver(moduleCatalog, moduleBuilder, routeCache, trie);
            var responseProcessors = new List<IResponseProcessor>() { new ResponseProcessor() };

            var negotiator = new ResponseNegotiator();
            var routeInvoker = new RouteInvoker(negotiator);

            var dispatcher = new RequestDispatcher(routeResolver, responseProcessors, routeInvoker);
            var engine = new Engine(dispatcher);

            var host = new SelfHost(engine, new Uri("http://localhost:8888/greeting/"));
            host.Start();

            var navigateTo = "http://localhost:8888/greeting/";
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
