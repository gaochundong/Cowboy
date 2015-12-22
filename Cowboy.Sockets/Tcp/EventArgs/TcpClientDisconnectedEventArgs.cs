using System;

namespace Cowboy.Sockets
{
    public class TcpClientDisconnectedEventArgs : EventArgs
    {
        public TcpClientDisconnectedEventArgs(ISession session)
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
