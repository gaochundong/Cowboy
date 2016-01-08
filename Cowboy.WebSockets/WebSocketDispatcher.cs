using System;
using System.Net;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Cowboy.Buffer;

namespace Cowboy.Http.WebSockets
{
    public class WebSocketDispatcher
    {
        private WebSocketRouteResolver _routeResolver;
        private IBufferManager _bufferManager;

        public WebSocketDispatcher(WebSocketRouteResolver routeResolver, IBufferManager bufferManager)
        {
            if (routeResolver == null)
                throw new ArgumentNullException("routeResolver");
            if (routeResolver == null)
                throw new ArgumentNullException("routeResolver");
            _routeResolver = routeResolver;
            _bufferManager = bufferManager;
        }

        public async Task Dispatch(HttpListenerContext httpContext, HttpListenerWebSocketContext webSocketContext, CancellationToken cancellationToken)
        {
            var module = _routeResolver.Resolve(webSocketContext);
            if (module != null)
            {
                var session = new WebSocketSession(module, httpContext, webSocketContext, cancellationToken, _bufferManager);
                await module.AcceptSession(session);
            }
        }
    }
}
