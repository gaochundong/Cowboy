using System.Threading.Tasks;
using Cowboy.Http.Responses;

namespace Cowboy.Http.Routing
{
    public class NotFoundRoute : Route
    {
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
