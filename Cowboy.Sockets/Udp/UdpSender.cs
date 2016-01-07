using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public class UdpSender : IDisposable
    {
        private readonly UdpClient _udpClient;
        private bool _disposed = false;

        public UdpSender()
        {
            _udpClient = new UdpClient();
        }

        public int Available { get { return _udpClient.Available; } }
        public Socket Client { get { return _udpClient.Client; } }
        public bool DontFragment { get { return _udpClient.DontFragment; } set { _udpClient.DontFragment = value; } }
        public bool EnableBroadcast { get { return _udpClient.EnableBroadcast; } set { _udpClient.EnableBroadcast = value; } }
        public bool ExclusiveAddressUse { get { return _udpClient.ExclusiveAddressUse; } set { _udpClient.ExclusiveAddressUse = value; } }
        public bool MulticastLoopback { get { return _udpClient.MulticastLoopback; } set { _udpClient.MulticastLoopback = value; } }
        public short Ttl { get { return _udpClient.Ttl; } set { _udpClient.Ttl = value; } }

        public void AllowNatTraversal(bool allowed)
        {
            _udpClient.AllowNatTraversal(allowed);
        }

        public void Close()
        {
            _udpClient.Close();
        }

        public async Task<int> Send(byte[] datagram, int bytes, IPEndPoint endPoint)
        {
            return await _udpClient.SendAsync(datagram, bytes, endPoint);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!this._disposed)
            {
                if (disposing)
                {
                    if (_udpClient != null)
                    {
                        _udpClient.Dispose();
                    }
                }

                _disposed = true;
            }
        }
    }
}
