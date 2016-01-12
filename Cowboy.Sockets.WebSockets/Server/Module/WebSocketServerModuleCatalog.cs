using System;
using System.Collections.Generic;

namespace Cowboy.Sockets.WebSockets
{
    public class WebSocketServerModuleCatalog
    {
        private Dictionary<string, WebSocketServerModule> _modules = new Dictionary<string, WebSocketServerModule>();

        public IEnumerable<WebSocketServerModule> GetAllModules()
        {
            return _modules.Values;
        }

        public WebSocketServerModule GetModule(Type moduleType)
        {
            return _modules[moduleType.FullName];
        }

        public void RegisterModule(WebSocketServerModule module)
        {
            _modules.Add(module.GetType().FullName, module);
        }
    }
}
