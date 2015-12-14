using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Routing;
using Cowboy.WebSockets;

namespace Cowboy
{
    public class Engine
    {
        private ContextFactory _contextFactory;
        private StaticContentProvider _staticContentProvider;
        private RequestDispatcher _requestDispatcher;
        private WebSocketDispatcher _webSocketDispatcher;

        public Engine(
            ContextFactory contextFactory,
            StaticContentProvider staticContentProvider,
            RequestDispatcher requestDispatcher,
            WebSocketDispatcher webSocketDispatcher)
        {
            if (contextFactory == null)
                throw new ArgumentNullException("contextFactory");
            if (staticContentProvider == null)
                throw new ArgumentNullException("staticContentProvider");
            if (requestDispatcher == null)
                throw new ArgumentNullException("requestDispatcher");
            if (webSocketDispatcher == null)
                throw new ArgumentNullException("webSocketDispatcher");

            _contextFactory = contextFactory;
            _staticContentProvider = staticContentProvider;
            _requestDispatcher = requestDispatcher;
            _webSocketDispatcher = webSocketDispatcher;
        }

        public async Task<Context> HandleRequest(Request request, CancellationToken cancellationToken)
        {
            if (request == null)
                throw new ArgumentNullException("request");

            var context = _contextFactory.Create(request);

            var staticContentResponse = _staticContentProvider.GetContent(context);
            if (staticContentResponse != null)
            {
                context.Response = staticContentResponse;
                return context;
            }

            context.Response = await _requestDispatcher.Dispatch(context, cancellationToken);

            return context;
        }

        public async Task HandleWebSocket(WebSocketContext context, CancellationToken cancellationToken)
        {
            await _webSocketDispatcher.Dispatch(context, cancellationToken);
        }
    }
}
