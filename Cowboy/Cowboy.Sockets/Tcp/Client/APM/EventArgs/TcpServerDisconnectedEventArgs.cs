using System;
using System.Net;

namespace Cowboy.Sockets
{
    public class TcpServerDisconnectedEventArgs : EventArgs
    {
        public TcpServerDisconnectedEventArgs(IPEndPoint remoteEP)
            : this(remoteEP, null)
        {
        }

        public TcpServerDisconnectedEventArgs(IPEndPoint remoteEP, IPEndPoint localEP)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");

            this.RemoteEndPoint = remoteEP;
            this.LocalEndPoint = localEP;
        }

        public IPEndPoint RemoteEndPoint { get; private set; }
        public IPEndPoint LocalEndPoint { get; private set; }

        public override string ToString()
        {
            return this.RemoteEndPoint.ToString();
        }
    }
}
