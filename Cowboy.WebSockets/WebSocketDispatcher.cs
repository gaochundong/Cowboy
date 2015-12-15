using System;
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

        public async Task Dispatch(WebSocketContext context, CancellationToken cancellationToken)
        {
            var module = _routeResolver.Resolve(context);
            if (module != null)
            {
                var session = new WebSocketSession(context, cancellationToken);
                await module.AcceptSession(session);
            }
        }
    }
}
