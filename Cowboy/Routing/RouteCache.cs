using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Routing
{
    public class RouteCache : Dictionary<Type, List<Tuple<int, RouteDescription>>>, IRouteCache
    {
        private readonly RouteSegmentExtractor routeSegmentExtractor;
        private readonly RouteDescriptionProvider routeDescriptionProvider;
        private readonly ModuleCatalog moduleCatalog;

        public RouteCache(
            RouteSegmentExtractor routeSegmentExtractor,
            RouteDescriptionProvider routeDescriptionProvider,
            ModuleCatalog moduleCatalog)
        {
            this.routeSegmentExtractor = routeSegmentExtractor;
            this.routeDescriptionProvider = routeDescriptionProvider;
            this.moduleCatalog = moduleCatalog;

            var modules = moduleCatalog.GetAllModules();
            this.BuildCache(modules);
        }

        private void BuildCache(IEnumerable<Module> modules)
        {
            foreach (var module in modules)
            {
                var moduleType = module.GetType();

                var routes = module.Routes.Select(r => r.Description).ToArray();

                foreach (var routeDescription in routes)
                {
                    routeDescription.Description = this.routeDescriptionProvider.GetDescription(module, routeDescription.Path);
                    routeDescription.Segments = this.routeSegmentExtractor.Extract(routeDescription.Path).ToArray();
                }

                this.AddRoutesToCache(routes, moduleType);
            }
        }

        private void AddRoutesToCache(IEnumerable<RouteDescription> routes, Type moduleType)
        {
            if (!this.ContainsKey(moduleType))
            {
                this[moduleType] = new List<Tuple<int, RouteDescription>>();
            }

            this[moduleType].AddRange(routes.Select((r, i) => new Tuple<int, RouteDescription>(i, r)));
        }

        public bool IsEmpty()
        {
            return !this.Values.SelectMany(r => r).Any();
        }
    }
}
