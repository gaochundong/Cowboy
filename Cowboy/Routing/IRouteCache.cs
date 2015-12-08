using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Routing
{
    public interface IRouteCache : IDictionary<Type, List<Tuple<int, RouteDescription>>>
    {
        bool IsEmpty();
    }
}
