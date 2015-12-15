using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public class WebSocketDispatcher
    {
        private WebSocketRouteResolver _routeResolver;

        public WebSocketDispatcher(WebSocketRouteResolver routeResolver)
        {
            if (routeResolver == null)
                throw new ArgumentNullException("routeResolver");
            _routeResolver = routeResolver;
        }

        public async Task Dispatch(HttpListenerContext httpContext, HttpListenerWebSocketContext webSocketContext, CancellationToken cancellationToken)
        {
            var module = _routeResolver.Resolve(webSocketContext);
            if (module != null)
            {
                var session = new WebSocketSession(httpContext, webSocketContext, cancellationToken);
                await module.AcceptSession(session);
            }
        }
    }
}
