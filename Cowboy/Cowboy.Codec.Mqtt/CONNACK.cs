using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Codec.Mqtt
{
    public sealed class CONNACK : ControlPacket
    {
        public CONNACK()
        {
        }

        public override ControlPacketType ControlPacketType { get { return ControlPacketType.CONNACK; } }

        protected override List<byte> GetVariableHeaderBytes()
        {
            var variableHeaderBytes = new List<byte>();

            byte connectAcknowledgeFlags = 0x0;

            if (this.SessionPresentFlag)
                connectAcknowledgeFlags = (byte)(connectAcknowledgeFlags | 0x01);

            variableHeaderBytes.Add(connectAcknowledgeFlags);

            variableHeaderBytes.Add((byte)(this.ConnectReturnCode));

            return variableHeaderBytes;
        }

        protected override List<byte> GetPayloadBytes()
        {
            return null; // The CONNACK Packet has no payload.
        }

        public ConnectReturnCodeValues ConnectReturnCode { get; set; }

        public bool SessionPresentFlag { get; set; }

        public enum ConnectReturnCodeValues
        {
            ConnectionAccepted = 0,
            UnacceptableProtocolVersion = 1,
            IdentifierRejected = 2,
            ServerUnavailable = 3,
            BadUserNameOrPassword = 4,
            NotAuthorized = 5,
        }
    }
}
