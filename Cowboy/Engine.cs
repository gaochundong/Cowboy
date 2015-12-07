using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Routing;

namespace Cowboy
{
    public class Engine
    {
        private RequestDispatcher _dispatcher;

        public Engine(RequestDispatcher dispatcher)
        {
            _dispatcher = dispatcher;
        }

        public async Task<Context> HandleRequest(Request request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request", "The request parameter cannot be null.");
            }

            var context = new Context()
            {
                Request = request,
            };

            // var tcs = new TaskCompletionSource<Context>();
            //var staticContentResponse = this.staticContentProvider.GetContent(context);
            //if (staticContentResponse != null)
            //{
            //    context.Response = staticContentResponse;
            //    tcs.SetResult(context);
            //    return tcs.Task;
            //}

            //var pipelines = this.RequestPipelinesFactory.Invoke(context);

            //var lifeCycleTask = this.InvokeRequestLifeCycle(context, cancellationToken, pipelines);
            context.Response = await _dispatcher.Dispatch(context, cancellationToken);

            return context;
        }

    }
}
