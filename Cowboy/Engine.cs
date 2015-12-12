using System;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Routing;

namespace Cowboy
{
    public class Engine
    {
        private ContextFactory _contextFactory;
        private StaticContentProvider _staticContentProvider;
        private RequestDispatcher _dispatcher;

        public Engine(ContextFactory contextFactory, StaticContentProvider staticContentProvider, RequestDispatcher dispatcher)
        {
            _contextFactory = contextFactory;
            _staticContentProvider = staticContentProvider;
            _dispatcher = dispatcher;
        }

        public async Task<Context> HandleRequest(Request request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                throw new ArgumentNullException("request", "The request parameter cannot be null.");
            }

            var context = _contextFactory.Create(request);

            var staticContentResponse = _staticContentProvider.GetContent(context);
            if (staticContentResponse != null)
            {
                context.Response = staticContentResponse;
                return context;
            }

            context.Response = await _dispatcher.Dispatch(context, cancellationToken);

            return context;
        }

    }
}
