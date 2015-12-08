using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy
{
    public class ContextFactory
    {
        public ContextFactory()
        {
        }

        public Context Create(Request request)
        {
            var context = new Context();

            context.Request = request;

            return context;
        }
    }
}
