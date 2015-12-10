using System;
using System.Collections.Generic;

namespace Cowboy.Routing
{
    public interface IRouteCache : IDictionary<Type, List<Tuple<int, RouteDescription>>>
    {
        bool IsEmpty();
    }
}
