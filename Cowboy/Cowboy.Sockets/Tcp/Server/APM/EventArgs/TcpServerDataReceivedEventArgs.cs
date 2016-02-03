using System;

namespace Cowboy.Sockets
{
    public class TcpServerDataReceivedEventArgs : EventArgs
    {
        public TcpServerDataReceivedEventArgs(TcpSocketClient client, byte[] data)
            : this(client, data, 0, data.Length)
        {
        }

        public TcpServerDataReceivedEventArgs(TcpSocketClient client, byte[] data, int dataOffset, int dataLength)
        {
            Client = client;
            Data = data;
            DataOffset = dataOffset;
            DataLength = dataLength;
        }

        public TcpSocketClient Client { get; private set; }
        public byte[] Data { get; private set; }
        public int DataOffset { get; private set; }
        public int DataLength { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}", this.Client);
        }
    }
}
