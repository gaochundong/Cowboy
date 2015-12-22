using System;
using System.Net;

namespace Cowboy.Sockets
{
    public class TcpServerDisconnectedEventArgs : EventArgs
    {
        public TcpServerDisconnectedEventArgs(IPEndPoint remoteEP)
        {
            if (remoteEP == null)
                throw new ArgumentNullException("remoteEP");

            this.RemoteEP = remoteEP;
        }

        public IPEndPoint RemoteEP { get; private set; }

        public override string ToString()
        {
            return this.RemoteEP.ToString();
        }
    }
}
