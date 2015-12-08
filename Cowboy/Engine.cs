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
        private ContextFactory _contextFactory;
        private RequestDispatcher _dispatcher;

        public Engine(ContextFactory contextFactory, RequestDispatcher dispatcher)
        {
            _contextFactory = contextFactory;
            _dispatcher = dispatcher;
        }

        public async Task<Context> HandleRequest(Request request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request", "The request parameter cannot be null.");
            }

            var context = _contextFactory.Create(request);
            context.Response = await _dispatcher.Dispatch(context, cancellationToken);

            return context;
        }

    }
}
