using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Routing
{
    public class ModuleCatalog
    {
        public IEnumerable<Module> GetAllModules(Context context)
        {
            return _modules;
        }

        //Module GetModule(Type moduleType, Context context);

        public static List<Module> _modules = new List<Module>();
    }
}
