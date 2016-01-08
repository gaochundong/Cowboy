using System;
using System.Linq;
using System.Net.WebSockets;

namespace Cowboy.Http.WebSockets
{
    public class WebSocketRouteResolver
    {
        private WebSocketModuleCatalog _moduleCatalog;

        public WebSocketRouteResolver(WebSocketModuleCatalog moduleCatalog)
        {
            if (moduleCatalog == null)
                throw new ArgumentNullException("moduleCatalog");
            _moduleCatalog = moduleCatalog;
        }

        public WebSocketModule Resolve(WebSocketContext context)
        {
            var modules = _moduleCatalog.GetAllModules();
            return modules.FirstOrDefault(m => m.ModulePath == context.RequestUri.AbsolutePath);
        }
    }
}
