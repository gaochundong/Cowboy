using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Codec.Mqtt
{
    public sealed class CONNECT : ControlPacket
    {
        public static readonly IDictionary<string, byte> VersionLevels = new Dictionary<string, byte>()
        {
            { "3.1.1", 0x04 },
        };

        public CONNECT()
        {
            this.ProtocolName = "MQTT";
            this.ProtocolVersion = "3.1.1";
            this.KeepAliveInterval = TimeSpan.FromSeconds(30);
            this.CleanSession = true;
        }

        public override ControlPacketType ControlPacketType { get { return ControlPacketType.CONNECT; } }

        public void BuildPayload()
        {
            var variableHeaderBytes = GetVariableHeaderBytes();

            var payloadBytes = GetPayloadBytes();

            byte fixedHeaderByte = (byte)((byte)this.ControlPacketType << 4);
            var remainingLengthBytes = GetRemainingLengthBytes(variableHeaderBytes.Count, payloadBytes.Length);
        }

        private List<byte> GetRemainingLengthBytes(int variableHeaderLength, int payloadLength)
        {
            var remainingLengthBytes = new List<byte>();
            int totalLength = variableHeaderLength + payloadLength;

            do
            {
                int encodedByte = totalLength % 128;
                totalLength = totalLength / 128;
                if (totalLength > 0)
                {
                    encodedByte = encodedByte | 0x80;
                }
                remainingLengthBytes.Add((byte)encodedByte);
            }
            while (totalLength > 0);

            return remainingLengthBytes;
        }

        private List<byte> GetVariableHeaderBytes()
        {
            var variableHeaderBytes = new List<byte>();

            if (string.IsNullOrWhiteSpace(this.ProtocolName))
                throw new NotSupportedException(
                    string.Format("Invalid protocol name [{0}].", this.ProtocolName));
            var protocolNameBytes = Encoding.UTF8.GetBytes(this.ProtocolName);
            variableHeaderBytes.Add((byte)(protocolNameBytes.Length >> 8));
            variableHeaderBytes.Add((byte)(protocolNameBytes.Length & 0xFF));
            variableHeaderBytes.AddRange(protocolNameBytes);

            if (string.IsNullOrWhiteSpace(this.ProtocolVersion))
                throw new NotSupportedException(
                    string.Format("Invalid protocol version [{0}].", this.ProtocolVersion));
            if (!VersionLevels.ContainsKey(this.ProtocolVersion))
                throw new NotSupportedException(
                    string.Format("Cannot support version [{0} {1}].", this.ProtocolName, this.ProtocolVersion));
            var protocolLevel = VersionLevels[this.ProtocolVersion];
            variableHeaderBytes.Add(protocolLevel);

            byte connectFlags = 0x0;

            if (this.UserNameFlag)
                connectFlags = (byte)(connectFlags | 0x80);
            if (this.PasswordFlag)
                connectFlags = (byte)(connectFlags | 0x40);
            if (this.WillRetain)
                connectFlags = (byte)(connectFlags | 0x20);
            if (this.WillFlag)
                connectFlags = (byte)(connectFlags | 0x04);
            if (this.CleanSession)
                connectFlags = (byte)(connectFlags | 0x02);

            switch (this.WillQos)
            {
                case WillQosLevel.QoS0:
                    break;
                case WillQosLevel.QoS1:
                    connectFlags = (byte)(connectFlags | 0x08);
                    break;
                case WillQosLevel.QoS2:
                    connectFlags = (byte)(connectFlags | 0x10);
                    break;
                default:
                    break;
            }

            variableHeaderBytes.Add(connectFlags);

            short keepAliveSeconds = (short)this.KeepAliveInterval.TotalSeconds;
            variableHeaderBytes.Add((byte)(keepAliveSeconds >> 8));
            variableHeaderBytes.Add((byte)(keepAliveSeconds & 0xFF));

            return variableHeaderBytes;
        }

        private byte[] GetPayloadBytes()
        {
            throw new NotImplementedException();
        }

        public string ClientIdentifier { get; set; } // required
        public string WillTopic { get; set; } // optional, determined based on flags
        public string WillMessage { get; set; } // optional, determined based on flags
        public string UserName { get; set; } // optional, determined based on flags
        public string Password { get; set; } // optional, determined based on flags

        public string ProtocolName { get; set; }
        public string ProtocolVersion { get; set; }
        public byte ConnectFlags { get; set; }
        public TimeSpan KeepAliveInterval { get; set; }

        public bool UserNameFlag { get; set; }
        public bool PasswordFlag { get; set; }
        public bool WillRetain { get; set; }
        public WillQosLevel WillQos { get; set; }
        public bool WillFlag { get; set; }
        public bool CleanSession { get; set; }

        public enum WillQosLevel
        {
            QoS0 = 0,
            QoS1 = 1,
            QoS2 = 2,
        }
    }
}
