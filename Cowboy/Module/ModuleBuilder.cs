using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cowboy.Routing
{
    public class ModuleBuilder
    {
        private readonly ResponseFormatterFactory responseFormatterFactory;

        public ModuleBuilder(ResponseFormatterFactory responseFormatterFactory)
        {
            this.responseFormatterFactory = responseFormatterFactory;
        }

        public Module BuildModule(Module module, Context context)
        {
            module.Context = context;
            module.Response = this.responseFormatterFactory.Create(context);

            return module;
        }
    }
}
