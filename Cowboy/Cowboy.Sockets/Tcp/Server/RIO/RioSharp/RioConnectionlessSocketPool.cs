using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Cowboy.Sockets.Experimental
{
    public class RioConnectionlessSocketPool : RioSocketPool
    {
        public RioConnectionlessSocketPool(RioFixedBufferPool sendPool, RioFixedBufferPool revicePool, int socketCount,
            uint maxOutstandingReceive = 1024, uint maxOutstandingSend = 1024, uint maxOutsandingCompletions = 1024)
            : base(sendPool, revicePool, maxOutstandingReceive, maxOutstandingSend, maxOutsandingCompletions)
        {

        }

        public RioConnectionlessSocket BindUdpSocket()
        {
            return new RioConnectionlessSocket(this,SendBufferPool,ReceiveBufferPool,MaxOutstandingReceive,MaxOutstandingSend,SendCompletionQueue,ReceiveCompletionQueue);           
        }
    }
}
