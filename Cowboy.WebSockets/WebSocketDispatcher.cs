using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.WebSockets
{
    public class WebSocketDispatcher
    {
        public WebSocketDispatcher()
        {

        }

        public async Task Dispatch(WebSocketContext context, CancellationToken cancellationToken)
        {
            var session = new WebSocketSession(context, cancellationToken);
            await session.Start();
        }
    }
}
