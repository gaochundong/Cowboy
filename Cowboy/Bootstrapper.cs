using System;
using System.Collections.Generic;
using Cowboy.Buffer;
using Cowboy.Http;
using Cowboy.Http.Responses;
using Cowboy.Http.Routing;
using Cowboy.Http.Routing.Trie;
using Cowboy.Http.WebSockets;
using Cowboy.Serialization;
using Cowboy.StaticContent;

namespace Cowboy
{
    public class Bootstrapper
    {
        public Bootstrapper()
        {
            this.Modules = new List<Module>();
            this.WebSocketModules = new List<WebSocketModule>();
        }

        public List<Module> Modules { get; private set; }
        public List<WebSocketModule> WebSocketModules { get; private set; }

        public Engine Boot()
        {
            var staticContentProvider = BuildStaticContentProvider();
            var requestDispatcher = BuildRequestDispatcher();
            var webSocketDispatcher = BuildWebSocketDispatcher();

            return new Engine(staticContentProvider, requestDispatcher, webSocketDispatcher);
        }

        private StaticContentProvider BuildStaticContentProvider()
        {
            var rootPathProvider = new RootPathProvider();
            var staticContnetConventions = new StaticContentsConventions(new List<Func<Context, string, Response>>
            {
                StaticContentConventionBuilder.AddDirectory("Content")
            });
            var staticContentProvider = new StaticContentProvider(rootPathProvider, staticContnetConventions);

            FileResponse.SafePaths.Add(rootPathProvider.GetRootPath());

            return staticContentProvider;
        }

        private RequestDispatcher BuildRequestDispatcher()
        {
            var moduleCatalog = new ModuleCatalog();
            foreach (var module in Modules)
            {
                moduleCatalog.RegisterModule(module);
            }

            var routeSegmentExtractor = new RouteSegmentExtractor();
            var routeDescriptionProvider = new RouteDescriptionProvider();
            var routeCache = new RouteCache(routeSegmentExtractor, routeDescriptionProvider);
            routeCache.BuildCache(moduleCatalog.GetAllModules());

            var trieNodeFactory = new TrieNodeFactory();
            var routeTrie = new RouteResolverTrie(trieNodeFactory);
            routeTrie.BuildTrie(routeCache);

            var serializers = new List<ISerializer>() { new JsonSerializer(), new XmlSerializer() };
            var responseFormatterFactory = new ResponseFormatterFactory(serializers);
            var moduleBuilder = new ModuleBuilder(responseFormatterFactory);

            var routeResolver = new RouteResolver(moduleCatalog, moduleBuilder, routeTrie);

            var negotiator = new ResponseNegotiator();
            var routeInvoker = new RouteInvoker(negotiator);
            var requestDispatcher = new RequestDispatcher(routeResolver, routeInvoker);

            return requestDispatcher;
        }

        private WebSocketDispatcher BuildWebSocketDispatcher()
        {
            var moduleCatalog = new WebSocketModuleCatalog();
            foreach (var module in WebSocketModules)
            {
                moduleCatalog.RegisterModule(module);
            }

            var routeResolver = new WebSocketRouteResolver(moduleCatalog);
            var bufferManager = new GrowingByteBufferManager(100, 64);

            return new WebSocketDispatcher(routeResolver, bufferManager);
        }
    }
}
