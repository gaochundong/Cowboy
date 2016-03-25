using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Codec.Mqtt
{
    public sealed class CONNECT : ControlPacket
    {
        public CONNECT()
        {
            this.ProtocolName = Encoding.UTF8.GetBytes("MQTT");
            this.ProtocolLevel = 0x04; // "3.1.1";
        }

        public override ControlPacketType ControlPacketType
        {
            get
            {
                return ControlPacketType.CONNECT;
            }
        }



        public string ClientIdentifier { get; set; } // required
        public string WillTopic { get; set; } // optional
        public string WillMessage { get; set; } // optional
        public string UserName { get; set; } // optional
        public string Password { get; set; } // optional

        public byte[] ProtocolName { get; set; }
        public byte ProtocolLevel { get; set; }
        public byte ConnectFlags { get; set; }
        public bool KeepAlive { get; set; }

        public bool UserNameFlag { get; set; }
        public bool PasswordFlag { get; set; }
        public bool WillRetain { get; set; }
        public WillQosLevel WillQos { get; set; }
        public bool WillFlag { get; set; }
        public bool CleanSession { get; set; }

        //public byte[] FixedHeader;
        //public byte[] Payload;
        //public byte[] VariableHeader;

        public enum WillQosLevel
        {
            QoS0 = 0,
            QoS1 = 1,
            QoS2 = 2,
        }
    }
}
