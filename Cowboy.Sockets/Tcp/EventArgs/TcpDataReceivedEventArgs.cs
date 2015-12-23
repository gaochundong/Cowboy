using System;

namespace Cowboy.Sockets
{
    public class TcpDataReceivedEventArgs : EventArgs
    {
        public TcpDataReceivedEventArgs(TcpSocketSession session, byte[] data)
            : this(session, data, 0, data.Length)
        {
        }

        public TcpDataReceivedEventArgs(TcpSocketSession session, byte[] data, int dataOffset, int dataLength)
        {
            Session = session;
            Data = data;
            DataOffset = dataOffset;
            DataLength = dataLength;
        }

        public TcpSocketSession Session { get; private set; }
        public byte[] Data { get; private set; }
        public int DataOffset { get; private set; }
        public int DataLength { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}", this.Session);
        }
    }
}
