using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Routing;

namespace Cowboy
{
    public class Context
    {
        public Context()
        {

        }

        public Request Request { get; set; }

        public Response Response { get; set; }

        public Route ResolvedRoute { get; set; }

        public dynamic Parameters { get; set; }
    }
}
