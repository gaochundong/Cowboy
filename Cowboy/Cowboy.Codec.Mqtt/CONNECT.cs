using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Codec.Mqtt
{
    public sealed class CONNECT : ControlPacket
    {
        public override ControlPacketType ControlPacketType
        {
            get
            {
                return ControlPacketType.CONNECT;
            }
        }

        public override byte[] FixedHeader
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override byte[] Payload
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }

        public override byte[] VariableHeader
        {
            get
            {
                throw new NotImplementedException();
            }

            set
            {
                throw new NotImplementedException();
            }
        }
    }
}
