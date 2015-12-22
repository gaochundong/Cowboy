using System;
using System.Globalization;
using System.Net;

namespace Cowboy.Sockets
{
    public class TcpServerExceptionOccurredEventArgs : EventArgs
    {
        public TcpServerExceptionOccurredEventArgs(IPAddress[] ipAddresses, int port, Exception innerException)
        {
            if (ipAddresses == null)
                throw new ArgumentNullException("ipAddresses");

            this.Addresses = ipAddresses;
            this.Port = port;
            this.Exception = innerException;
        }

        public IPAddress[] Addresses { get; private set; }
        public int Port { get; private set; }
        public Exception Exception { get; private set; }

        public override string ToString()
        {
            string s = string.Empty;
            foreach (var item in Addresses)
            {
                s = s + item.ToString() + ',';
            }
            s = s.TrimEnd(',');
            s = s + ":" + Port.ToString(CultureInfo.InvariantCulture);

            return s;
        }
    }
}
