using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Routing
{
    public class ModuleCatalog
    {
        private List<Module> _modules = new List<Module>();

        public IEnumerable<Module> GetAllModules()
        {
            return _modules;
        }

        public void RegisterModule(Module module)
        {
            _modules.Add(module);
        }
    }
}
