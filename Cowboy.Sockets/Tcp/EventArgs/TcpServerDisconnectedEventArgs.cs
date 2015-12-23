using System;
using System.Net;

namespace Cowboy.Sockets
{
    public class TcpServerDisconnectedEventArgs : EventArgs
    {
        public TcpServerDisconnectedEventArgs(EndPoint remoteEP, EndPoint localEP)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");

            this.RemoteEndPoint = remoteEP;
            this.LocalEndPoint = localEP;
        }

        public EndPoint RemoteEndPoint { get; private set; }
        public EndPoint LocalEndPoint { get; private set; }

        public override string ToString()
        {
            return this.RemoteEndPoint.ToString();
        }
    }
}
