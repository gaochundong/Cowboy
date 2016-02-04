using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cowboy.Sockets.Experimental
{
    public class RioConnectionlessSocket : RioSocketBase
    {
        internal RioConnectionlessSocket(RioSocketPool pool, RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool,
            uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue) :
            base(sendBufferPool, receiveBufferPool, maxOutstandingReceive, maxOutstandingSend,
                SendCompletionQueue, ReceiveCompletionQueue,
                ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_DGRAM, PROTOCOL.IPPROTO_UDP)
        {

        }


        public bool IsBroadcast { get; set; }
    }
}
