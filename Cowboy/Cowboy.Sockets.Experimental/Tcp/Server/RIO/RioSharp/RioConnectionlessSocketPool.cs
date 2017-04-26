using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace RioSharp
{
    public class RioConnectionlessSocketPool : RioSocketPool
    {
        public RioConnectionlessSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, ADDRESS_FAMILIES adressFam, SOCKET_TYPE sockType, PROTOCOL protocol,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxOutsandingCompletions = 1024)
            : base(sendPool, revicePool, adressFam, sockType, protocol, maxOutstandingReceive, maxOutstandingSend, maxOutsandingCompletions)
        {

        }

        public RioSocket BindUdpSocket()
        {
            return new RioConnectionlessSocket(this,SendBufferPool,ReceiveBufferPool,MaxOutstandingReceive,MaxOutstandingSend,SendCompletionQueue,ReceiveCompletionQueue, adressFam, sockType, protocol);           
        }
    }
}
