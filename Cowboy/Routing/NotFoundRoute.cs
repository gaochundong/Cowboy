using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Routing
{
    /// <summary>
    /// Route that is returned when the path could not be matched.
    /// </summary>
    /// <remarks>This is equal to sending back the 404 HTTP status code.</remarks>
    public class NotFoundRoute : Route
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NotFoundRoute"/> type, for the
        /// specified <paramref name="path"/> and <paramref name="method"/>.
        /// </summary>
        /// <param name="method">The HTTP method of the route.</param>
        /// <param name="path">The path of the route.</param>
        public NotFoundRoute(string method, string path)
            : base(method, path, null, (x, c) => 
                {
                    var tcs = new TaskCompletionSource<dynamic>();
                    tcs.SetResult(new NotFoundResponse());
                    return tcs.Task;
                })
        {
        }
    }
}
