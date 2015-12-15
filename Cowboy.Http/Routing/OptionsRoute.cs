using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Cowboy.Http.Routing
{
    /// <summary>
    /// Route that is returned when the path could be matched but, the method was OPTIONS and there was no user defined handler for OPTIONS.
    /// </summary>
    public class OptionsRoute : Route
    {
        public OptionsRoute(string path, IEnumerable<string> allowedMethods)
            : base("OPTIONS", path, null, (x, c) => CreateMethodOptionsResponse(allowedMethods))
        {
        }

        private static Task<dynamic> CreateMethodOptionsResponse(IEnumerable<string> allowedMethods)
        {
            var response = new Response();
            response.Headers["Allow"] = string.Join(", ", allowedMethods);
            response.StatusCode = HttpStatusCode.OK;

            var tcs = new TaskCompletionSource<dynamic>();
            tcs.SetResult(response);
            return tcs.Task;
        }
    }
}
