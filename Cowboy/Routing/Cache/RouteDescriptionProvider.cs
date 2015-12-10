using System;
using System.Linq;
using System.Resources;

namespace Cowboy.Routing
{
    public class RouteDescriptionProvider
    {
        public string GetDescription(Module module, string path)
        {
            var assembly = module.GetType().Assembly;

            if (assembly.IsDynamic)
            {
                return string.Empty;
            }

            var moduleName = string.Concat(module.GetType().FullName, ".resources");

            var resourceName = assembly
                .GetManifestResourceNames()
                .FirstOrDefault(x => x.Equals(moduleName, StringComparison.OrdinalIgnoreCase));

            if (resourceName != null)
            {
                var manager = new ResourceManager(resourceName.Replace(".resources", string.Empty), assembly);

                return manager.GetString(path);
            }

            return string.Empty;
        }
    }
}
