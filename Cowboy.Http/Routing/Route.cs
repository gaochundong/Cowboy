using System;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Http.Routing
{
    public class Route
    {
        public Route(RouteDescription description, Func<dynamic, CancellationToken, Task<dynamic>> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException("action");
            }

            this.Description = description;
            this.Action = action;
        }

        public Route(string name, string method, string path, Func<Context, bool> condition, Func<dynamic, CancellationToken, Task<dynamic>> action)
            : this(new RouteDescription(name, method, path, condition), action)
        {
        }

        public Route(string method, string path, Func<Context, bool> condition, Func<dynamic, CancellationToken, Task<dynamic>> action)
            : this(string.Empty, method, path, condition, action)
        {
        }

        public Func<dynamic, CancellationToken, Task<dynamic>> Action { get; set; }

        public RouteDescription Description { get; private set; }

        public async Task<dynamic> Invoke(DynamicDictionary parameters, CancellationToken cancellationToken)
        {
            return await this.Action.Invoke(parameters, cancellationToken);
        }

        public static Route FromSync(RouteDescription description, Func<dynamic, dynamic> syncFunc)
        {
            return new Route(description, Wrap(syncFunc));
        }

        public static Route FromSync(string method, string path, Func<Context, bool> condition, Func<dynamic, dynamic> syncFunc)
        {
            return FromSync(string.Empty, method, path, condition, syncFunc);
        }

        public static Route FromSync(string name, string method, string path, Func<Context, bool> condition, Func<dynamic, dynamic> syncFunc)
        {
            return FromSync(new RouteDescription(name, method, path, condition), syncFunc);
        }

        private static Func<dynamic, CancellationToken, Task<dynamic>> Wrap(Func<object, object> syncFunc)
        {
            return (parameters, context) =>
            {
                var tcs = new TaskCompletionSource<dynamic>();

                try
                {
                    var result = syncFunc.Invoke(parameters);

                    tcs.SetResult(result);
                }
                catch (Exception e)
                {
                    tcs.SetException(e);
                }

                return tcs.Task;
            };
        }
    }
}
