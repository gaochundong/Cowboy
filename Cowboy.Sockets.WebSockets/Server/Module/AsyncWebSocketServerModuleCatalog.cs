using System;
using System.Collections.Generic;

namespace Cowboy.WebSockets
{
    public class AsyncWebSocketServerModuleCatalog
    {
        private Dictionary<string, AsyncWebSocketServerModule> _modules = new Dictionary<string, AsyncWebSocketServerModule>();

        public IEnumerable<AsyncWebSocketServerModule> GetAllModules()
        {
            return _modules.Values;
        }

        public AsyncWebSocketServerModule GetModule(Type moduleType)
        {
            return _modules[moduleType.FullName];
        }

        public void RegisterModule(AsyncWebSocketServerModule module)
        {
            _modules.Add(module.GetType().FullName, module);
        }
    }
}
