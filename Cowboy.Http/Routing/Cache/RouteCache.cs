using System;
using System.Collections.Generic;
using System.Linq;

namespace Cowboy.Http.Routing
{
    public class RouteCache : Dictionary<Type, List<Tuple<int, RouteDescription>>>
    {
        private readonly RouteSegmentExtractor routeSegmentExtractor;
        private readonly RouteDescriptionProvider routeDescriptionProvider;

        public RouteCache(
            RouteSegmentExtractor routeSegmentExtractor,
            RouteDescriptionProvider routeDescriptionProvider)
        {
            this.routeSegmentExtractor = routeSegmentExtractor;
            this.routeDescriptionProvider = routeDescriptionProvider;
        }

        public void BuildCache(IEnumerable<Module> modules)
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
