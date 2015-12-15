using System;
using System.Collections.Generic;

namespace Cowboy.WebSockets
{
    public class WebSocketModuleCatalog
    {
        private Dictionary<string, WebSocketModule> _modules = new Dictionary<string, WebSocketModule>();

        public IEnumerable<WebSocketModule> GetAllModules()
        {
            return _modules.Values;
        }

        public WebSocketModule GetModule(Type moduleType)
        {
            return _modules[moduleType.FullName];
        }

        public void RegisterModule(WebSocketModule module)
        {
            _modules.Add(module.GetType().FullName, module);
        }
    }
}
