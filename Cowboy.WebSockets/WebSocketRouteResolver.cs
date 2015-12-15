using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
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
            //if (modules.Any(m => m.ModulePath == context.RequestUri))

            var module = modules.First();

            return module;
        }
    }
}
