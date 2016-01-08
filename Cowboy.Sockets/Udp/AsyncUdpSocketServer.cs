using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using Cowboy.Logging;

namespace Cowboy.Sockets
{
    public class AsyncUdpSocketServer : IDisposable
    {
        private static readonly ILog _log = Logger.Get<AsyncUdpSocketServer>();
        private readonly UdpClient _udpClient;
        private readonly IPEndPoint _localEP;
        private bool _disposed = false;

        public AsyncUdpSocketServer(int localPort)
            : this(new IPEndPoint(IPAddress.Any, localPort))
        {
        }

        public AsyncUdpSocketServer(IPEndPoint localEP)
        {
            if (localEP == null)
                throw new ArgumentNullException("localEP");
            _localEP = localEP;
            _udpClient = new UdpClient(localEP);
        }

        public IPEndPoint LocalEndPoint { get { return _localEP; } }
        public int Available { get { return _udpClient.Available; } }
        public Socket Client { get { return _udpClient.Client; } }
        public bool DontFragment { get { return _udpClient.DontFragment; } set { _udpClient.DontFragment = value; } }
        public bool EnableBroadcast { get { return _udpClient.EnableBroadcast; } set { _udpClient.EnableBroadcast = value; } }
        public bool ExclusiveAddressUse { get { return _udpClient.ExclusiveAddressUse; } set { _udpClient.ExclusiveAddressUse = value; } }
        public bool MulticastLoopback { get { return _udpClient.MulticastLoopback; } set { _udpClient.MulticastLoopback = value; } }
        public short Ttl { get { return _udpClient.Ttl; } set { _udpClient.Ttl = value; } }
        public void AllowNatTraversal(bool allowed) { _udpClient.AllowNatTraversal(allowed); }

        public void Close()
        {
            try
            {
                _udpClient.Close();
            }
            catch (Exception ex) when (ex is SocketException) { }
        }

        public async Task<UdpReceiveResult> Receive()
        {
            try
            {
                return await _udpClient.ReceiveAsync();
            }
            catch (Exception ex) when (ShouldClose(ex))
            {
                _log.Error(ex.Message, ex);
                Close();
            }

            return await Task.FromResult(new UdpReceiveResult(new byte[0], null));
        }

        public async Task<int> Send(byte[] datagram, int count, IPEndPoint remoteEP)
        {
            try
            {
                return await _udpClient.SendAsync(datagram, count, remoteEP);
            }
            catch (Exception ex) when (ShouldClose(ex))
            {
                _log.Error(ex.Message, ex);
                Close();
            }

            return 0;
        }

        private bool ShouldClose(Exception ex)
        {
            return (ex is SocketException || ex is InvalidOperationException || ex is ObjectDisposedException);
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
                    Close();
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
