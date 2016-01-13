using System;
using System.Linq;

namespace Cowboy.WebSockets
{
    public class AsyncWebSocketRouteResolver
    {
        private AsyncWebSocketServerModuleCatalog _moduleCatalog;

        public AsyncWebSocketRouteResolver(AsyncWebSocketServerModuleCatalog moduleCatalog)
        {
            if (moduleCatalog == null)
                throw new ArgumentNullException("moduleCatalog");
            _moduleCatalog = moduleCatalog;
        }

        public AsyncWebSocketServerModule Resolve(string path, string query)
        {
            var modules = _moduleCatalog.GetAllModules();
            return modules.FirstOrDefault(m =>
                string.Compare(
                    m.ModulePath.Trim().TrimStart('/').TrimEnd('/').ToLowerInvariant(),
                    path.Trim().TrimStart('/').TrimEnd('/').ToLowerInvariant(),
                    StringComparison.OrdinalIgnoreCase
                    ) == 0);
        }
    }
}
