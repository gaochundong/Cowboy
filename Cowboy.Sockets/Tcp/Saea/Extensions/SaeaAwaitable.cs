using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public sealed class SaeaAwaitable : IDisposable
    {
        internal static readonly byte[] EmptyArray = new byte[0];
        internal static readonly ArraySegment<byte> EmptyArraySegment = new ArraySegment<byte>(EmptyArray);
        private readonly SocketAsyncEventArgs _saea = new SocketAsyncEventArgs();
        private readonly object _sync = new object();
        private readonly SaeaAwaiter _awaiter;
        private ArraySegment<byte> _transferredBytes;
        private bool _isDisposed;
        private bool _shouldCaptureContext;

        public SaeaAwaitable()
        {
            _awaiter = new SaeaAwaiter(this);
            _transferredBytes = new ArraySegment<byte>(EmptyArray);
        }

        internal SocketAsyncEventArgs Saea
        {
            get { return _saea; }
        }

        public Socket AcceptSocket
        {
            get { return Saea.AcceptSocket; }
        }

        //public ArraySegment<byte> Buffer
        //{
        //    get
        //    {
        //        lock (_sync)
        //            return new ArraySegment<byte>(
        //                this.Saea.Buffer ?? EmptyArray,
        //                this.Saea.Offset,
        //                this.Saea.Count);
        //    }
        //    set
        //    {
        //        lock (_sync)
        //            this.Saea.SetBuffer(value.Array ?? EmptyArray, value.Offset, value.Count);
        //    }
        //}

        //public ArraySegment<byte> Transferred
        //{
        //    get { return _transferredBytes; }
        //    internal set { _transferredBytes = value; }
        //}

        //public Exception ConnectByNameError
        //{
        //    get { return this.Saea.ConnectByNameError; }
        //}

        //public bool DisconnectReuseSocket
        //{
        //    get { return this.Saea.DisconnectReuseSocket; }
        //    set { this.Saea.DisconnectReuseSocket = value; }
        //}

        //public SocketAsyncOperation LastOperation
        //{
        //    get { return this.Saea.LastOperation; }
        //}

        //public EndPoint RemoteEndPoint
        //{
        //    get { return this.Saea.RemoteEndPoint; }
        //    set { this.Saea.RemoteEndPoint = value; }
        //}

        //public SocketFlags SocketFlags
        //{
        //    get { return this.Saea.SocketFlags; }
        //    set { this.Saea.SocketFlags = value; }
        //}

        //public object UserToken
        //{
        //    get { return this.Saea.UserToken; }
        //    set { this.Saea.UserToken = value; }
        //}

        public bool ShouldCaptureContext
        {
            get
            {
                return _shouldCaptureContext;
            }
            set
            {
                lock (_awaiter.SyncRoot)
                    if (_awaiter.IsCompleted)
                        _shouldCaptureContext = value;
                    else
                        throw new InvalidOperationException(
                            "A socket operation is already in progress using the same await-able SAEA.");
            }
        }

        public void Clear()
        {
            this.Saea.AcceptSocket = null;
            this.Saea.SetBuffer(EmptyArray, 0, 0);
            //this.RemoteEndPoint = null;
            //this.SocketFlags = SocketFlags.None;
            //this.Transferred = EmptyArraySegment;
            //this.UserToken = null;
        }

        public SaeaAwaiter GetAwaiter()
        {
            return _awaiter;
        }

        public bool IsDisposed
        {
            get { return _isDisposed; }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                if (!IsDisposed)
                {
                    _saea.Dispose();
                    _isDisposed = true;
                }
            }
        }
    }
}
