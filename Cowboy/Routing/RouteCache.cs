using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Routing
{
    public class RouteCache : Dictionary<Type, List<Tuple<int, RouteDescription>>>, IRouteCache
    {
        private readonly RouteSegmentExtractor routeSegmentExtractor = new RouteSegmentExtractor();
        private readonly RouteDescriptionProvider routeDescriptionProvider = new RouteDescriptionProvider();
        private readonly IEnumerable<IRouteMetadataProvider> routeMetadataProviders = new List<IRouteMetadataProvider>();
        private ModuleCatalog moduleCatalog = new ModuleCatalog();

        public RouteCache()
        {
            var modules = moduleCatalog.GetAllModules(null);

            this.BuildCache(modules);
        }

        public bool IsEmpty()
        {
            return !this.Values.SelectMany(r => r).Any();
        }

        private void BuildCache(IEnumerable<Module> modules)
        {
            foreach (var module in modules)
            {
                var moduleType = module.GetType();

                var routes =
                    module.Routes.Select(r => r.Description).ToArray();

                foreach (var routeDescription in routes)
                {
                    routeDescription.Description = this.routeDescriptionProvider.GetDescription(module, routeDescription.Path);
                    routeDescription.Segments = this.routeSegmentExtractor.Extract(routeDescription.Path).ToArray();
                    routeDescription.Metadata = this.GetRouteMetadata(module, routeDescription);
                }

                this.AddRoutesToCache(routes, moduleType);
            }
        }

        private RouteMetadata GetRouteMetadata(Module module, RouteDescription routeDescription)
        {
            var data = new Dictionary<Type, object>();

            foreach (var provider in this.routeMetadataProviders)
            {
                var type = provider.GetMetadataType(module, routeDescription);
                var metadata = provider.GetMetadata(module, routeDescription);

                if (type != null && metadata != null)
                {
                    data.Add(type, metadata);
                }
            }

            return new RouteMetadata(data);
        }

        private void AddRoutesToCache(IEnumerable<RouteDescription> routes, Type moduleType)
        {
            if (!this.ContainsKey(moduleType))
            {
                this[moduleType] = new List<Tuple<int, RouteDescription>>();
            }

            this[moduleType].AddRange(routes.Select((r, i) => new Tuple<int, RouteDescription>(i, r)));
        }
    }
}
