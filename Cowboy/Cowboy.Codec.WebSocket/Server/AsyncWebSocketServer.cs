//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using Cowboy.Buffer;
//using Cowboy.Sockets;
//using Logrila.Logging;

//namespace Cowboy.Codec.WebSocket
//{
//    public sealed class AsyncWebSocketServer : IAsyncTcpSocketServerMessageDispatcher
//    {
//        private static readonly ILog _log = Logger.Get<AsyncWebSocketServer>();
//        private readonly IBufferManager _bufferManager;

//        public string RemoteEndPoint { get; internal set; }

//        public AsyncWebSocketServer()
//        {
//        }

//        public async Task OnSessionStarted(AsyncTcpSocketSession session)
//        {
//            //session.Negotiate(RetrieveNegotiationRequest, );
//        }

//        public async Task OnSessionDataReceived(AsyncTcpSocketSession session, byte[] data, int offset, int count)
//        {
//            //var handshaker = OpenHandshake();
//            //if (!handshaker.Wait(session.ConnectTimeout))
//            //{
//            //    throw new TimeoutException(string.Format(
//            //        "Handshake with remote [{0}] timeout [{1}].", session.RemoteEndPoint, session.ConnectTimeout));
//            //}
//            //if (!handshaker.Result)
//            //{
//            //    //var responseBuffer = WebSocketServerHandshaker.CreateOpenningHandshakeBadRequestResponse(this);
//            //    //await _stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);

//            //    //throw new WebSocketException(string.Format(
//            //    //    "Handshake with remote [{0}] failed.", session.RemoteEndPoint));
//            //}
//        }

//        public async Task OnSessionClosed(AsyncTcpSocketSession session)
//        {
//        }

//        private bool RetrieveNegotiationRequest(AsyncTcpSocketSession session, byte[] buffer, int count, out int requestLength)
//        {
//            requestLength = 0;

//            int terminatorIndex = -1;
//            var found = WebSocketHelpers.FindHttpMessageTerminator(buffer, count, out terminatorIndex);
//            if (found)
//            {
//                requestLength = terminatorIndex + Consts.HttpMessageTerminator.Length;
//            }

//            return found;
//        }

//        private byte[] BuildNegotiationResponse(AsyncTcpSocketSession session, byte[] buffer, int count)
//        {
//            string secWebSocketKey = string.Empty;
//            string path = string.Empty;
//            string query = string.Empty;
//            bool handshakeResult = WebSocketServerHandshaker.HandleOpenningHandshakeRequest(session,
//                buffer, 0, count,
//                out secWebSocketKey, out path, out query);

//            _module = _routeResolver.Resolve(path, query);
//            if (_module == null)
//            {
//                throw new WebSocketHandshakeException(string.Format(
//                    "Handshake with remote [{0}] failed due to cannot identify the resource name [{1}{2}].", session.RemoteEndPoint, path, query));
//            }

//            if (handshakeResult)
//            {
//                var responseBuffer = WebSocketServerHandshaker.CreateOpenningHandshakeResponse(this, secWebSocketKey);
//                return responseBuffer;
//            }

//            return null;
//        }
//    }
//}
