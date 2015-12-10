using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Cowboy.Routing
{
    public class MethodNotAllowedRoute : Route
    {
        public MethodNotAllowedRoute(string path, string method, IEnumerable<string> allowedMethods)
            : base(method, path, null, (x, c) => CreateMethodNotAllowedResponse(allowedMethods))
        {
        }

        private static Task<dynamic> CreateMethodNotAllowedResponse(IEnumerable<string> allowedMethods)
        {
            var response = new Response();
            response.Headers["Allow"] = string.Join(", ", allowedMethods);
            response.StatusCode = HttpStatusCode.MethodNotAllowed;

            var tcs = new TaskCompletionSource<dynamic>();
            tcs.SetResult(response);
            return tcs.Task;
        }
    }
}
