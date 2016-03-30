using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Cowboy.Sockets;

namespace Cowboy.Codec.WebSocket
{
    public sealed class AsyncWebSocketSession : IAsyncTcpSocketServerMessageDispatcher
    {
        public async Task OnSessionStarted(AsyncTcpSocketSession session)
        {
        }

        public async Task OnSessionDataReceived(AsyncTcpSocketSession session, byte[] data, int offset, int count)
        {
            //var handshaker = OpenHandshake();
            //if (!handshaker.Wait(session.ConnectTimeout))
            //{
            //    throw new TimeoutException(string.Format(
            //        "Handshake with remote [{0}] timeout [{1}].", session.RemoteEndPoint, session.ConnectTimeout));
            //}
            //if (!handshaker.Result)
            //{
            //    //var responseBuffer = WebSocketServerHandshaker.CreateOpenningHandshakeBadRequestResponse(this);
            //    //await _stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);

            //    //throw new WebSocketException(string.Format(
            //    //    "Handshake with remote [{0}] failed.", session.RemoteEndPoint));
            //}
        }

        public async Task OnSessionClosed(AsyncTcpSocketSession session)
        {
        }

        //private async Task<bool> OpenHandshake()
        //{
        //    bool handshakeResult = false;

        //    try
        //    {
        //        int terminatorIndex = -1;
        //        while (!WebSocketHelpers.FindHeaderTerminator(_receiveBuffer, _receiveBufferOffset, out terminatorIndex))
        //        {
        //            int receiveCount = await _stream.ReadAsync(_receiveBuffer, _receiveBufferOffset, _receiveBuffer.Length - _receiveBufferOffset);
        //            if (receiveCount == 0)
        //            {
        //                throw new WebSocketHandshakeException(string.Format(
        //                    "Handshake with remote [{0}] failed due to receive zero bytes.", RemoteEndPoint));
        //            }

        //            BufferDeflector.ReplaceBuffer(_bufferManager, ref _receiveBuffer, ref _receiveBufferOffset, receiveCount);

        //            if (_receiveBufferOffset > 2048)
        //            {
        //                throw new WebSocketHandshakeException(string.Format(
        //                    "Handshake with remote [{0}] failed due to receive weird stream.", RemoteEndPoint));
        //            }
        //        }

        //        string secWebSocketKey = string.Empty;
        //        string path = string.Empty;
        //        string query = string.Empty;
        //        handshakeResult = WebSocketServerHandshaker.HandleOpenningHandshakeRequest(this,
        //            _receiveBuffer, 0, terminatorIndex + Consts.HeaderTerminator.Length,
        //            out secWebSocketKey, out path, out query);

        //        _module = _routeResolver.Resolve(path, query);
        //        if (_module == null)
        //        {
        //            throw new WebSocketHandshakeException(string.Format(
        //                "Handshake with remote [{0}] failed due to cannot identify the resource name [{1}{2}].", RemoteEndPoint, path, query));
        //        }

        //        if (handshakeResult)
        //        {
        //            var responseBuffer = WebSocketServerHandshaker.CreateOpenningHandshakeResponse(this, secWebSocketKey);
        //            await _stream.WriteAsync(responseBuffer, 0, responseBuffer.Length);
        //        }

        //        BufferDeflector.ShiftBuffer(_bufferManager, terminatorIndex + Consts.HeaderTerminator.Length, ref _receiveBuffer, ref _receiveBufferOffset);
        //    }
        //    catch (ArgumentOutOfRangeException)
        //    {
        //        handshakeResult = false;
        //    }
        //    catch (WebSocketHandshakeException ex)
        //    {
        //        _log.Error(string.Format("Session [{0}] exception occurred, [{1}].", this, ex.Message), ex);
        //        handshakeResult = false;
        //    }

        //    return handshakeResult;
        //}
    }
}
