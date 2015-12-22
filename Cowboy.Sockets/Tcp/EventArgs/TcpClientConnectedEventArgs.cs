using System;

namespace Cowboy.Sockets
{
    public class TcpClientConnectedEventArgs : EventArgs
    {
        public TcpClientConnectedEventArgs(ISession session)
        {
            if (session == null)
                throw new ArgumentNullException("session");

            this.Session = session;
        }

        public ISession Session { get; private set; }

        public override string ToString()
        {
            return string.Format("{0}", this.Session);
        }
    }
}
