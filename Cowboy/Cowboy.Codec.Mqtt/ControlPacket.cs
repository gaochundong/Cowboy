using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Codec.Mqtt
{
    public abstract class ControlPacket
    {
        public abstract ControlPacketType ControlPacketType { get; }

        public abstract byte[] FixedHeader { get; set; }

        public abstract byte[] VariableHeader { get; set; }

        public abstract byte[] Payload { get; set; }
    }
}
