using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    internal class RioConnectionlessSocket : RioSocket
    {
        internal RioConnectionlessSocket(RioSocketPool pool, RioFixedBufferPool sendBufferPool, RioFixedBufferPool receiveBufferPool,
            uint maxOutstandingReceive, uint maxOutstandingSend, IntPtr SendCompletionQueue, IntPtr ReceiveCompletionQueue,
            ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol) :
            base(sendBufferPool, receiveBufferPool, maxOutstandingReceive, maxOutstandingSend,
                SendCompletionQueue, ReceiveCompletionQueue,
                adressFam, sockType, protocol) //ADDRESS_FAMILIES.AF_INET, SOCKET_TYPE.SOCK_DGRAM, PROTOCOL.IPPROTO_UDP
        {

        }
    }
}
