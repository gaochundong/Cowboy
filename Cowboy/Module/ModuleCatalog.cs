using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Routing
{
    public class ModuleCatalog
    {
        private Dictionary<string, Module> _modules = new Dictionary<string, Module>();

        public IEnumerable<Module> GetAllModules()
        {
            return _modules.Values;
        }

        public Module GetModule(Type moduleType)
        {
            return _modules[moduleType.FullName];
        }

        public void RegisterModule(Module module)
        {
            _modules.Add(module.GetType().FullName, module);
        }
    }
}
