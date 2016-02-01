using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
//    class SocketConnectionListener : IConnectionListener
//    {
//        IPEndPoint localEndpoint;
//        bool isDisposed;
//        bool isListening;
//        Socket listenSocket;
//        ISocketListenerSettings settings;
//        bool useOnlyOverlappedIO;
//        ConnectionBufferPool connectionBufferPool;
//        SocketAsyncEventArgsPool socketAsyncEventArgsPool;

//        public SocketConnectionListener(Socket listenSocket, ISocketListenerSettings settings, bool useOnlyOverlappedIO)
//            : this(settings, useOnlyOverlappedIO)
//        {
//            this.listenSocket = listenSocket;
//        }

//        public SocketConnectionListener(IPEndPoint localEndpoint, ISocketListenerSettings settings, bool useOnlyOverlappedIO)
//            : this(settings, useOnlyOverlappedIO)
//        {
//            this.localEndpoint = localEndpoint;
//        }

//        SocketConnectionListener(ISocketListenerSettings settings, bool useOnlyOverlappedIO)
//        {
//            Fx.Assert(settings != null, "Input settings should not be null");
//            this.settings = settings;
//            this.useOnlyOverlappedIO = useOnlyOverlappedIO;
//            this.connectionBufferPool = new ConnectionBufferPool(settings.BufferSize);
//        }

//        object ThisLock
//        {
//            get { return this; }
//        }

//        public IAsyncResult BeginAccept(AsyncCallback callback, object state)
//        {
//            return new AcceptAsyncResult(this, callback, state);
//        }

//        SocketAsyncEventArgs TakeSocketAsyncEventArgs()
//        {
//            return this.socketAsyncEventArgsPool.Take();
//        }

//        void ReturnSocketAsyncEventArgs(SocketAsyncEventArgs socketAsyncEventArgs)
//        {
//            Fx.Assert(socketAsyncEventArgsPool != null, "The socketAsyncEventArgsPool should not be null");
//            this.socketAsyncEventArgsPool.Return(socketAsyncEventArgs);
//        }

//        // This is the buffer size that is used by the System.Net for accepting new connections
//        static int GetAcceptBufferSize(Socket listenSocket)
//        {
//            return (listenSocket.LocalEndPoint.Serialize().Size + 16) * 2;
//        }

//        bool InternalBeginAccept(Func<Socket, bool> acceptAsyncFunc)
//        {
//            lock (ThisLock)
//            {
//                if (isDisposed)
//                {
//                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new ObjectDisposedException(this.GetType().ToString(), SR.GetString(SR.SocketListenerDisposed)));
//                }

//                if (!isListening)
//                {
//                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(new InvalidOperationException(SR.GetString(SR.SocketListenerNotListening)));
//                }

//                return acceptAsyncFunc(listenSocket);
//            }
//        }

//        public IConnection EndAccept(IAsyncResult result)
//        {
//            Socket socket = AcceptAsyncResult.End(result);

//            if (socket == null)
//                return null;

//            if (useOnlyOverlappedIO)
//            {
//                socket.UseOnlyOverlappedIO = true;
//            }
//            return new SocketConnection(socket, this.connectionBufferPool, false);
//        }

//        public void Dispose()
//        {
//            lock (ThisLock)
//            {
//                if (!isDisposed)
//                {
//                    if (listenSocket != null)
//                    {
//                        listenSocket.Close();
//                    }

//                    if (this.socketAsyncEventArgsPool != null)
//                    {
//                        this.socketAsyncEventArgsPool.Close();
//                    }

//                    isDisposed = true;
//                }
//            }
//        }


//        public void Listen()
//        {
//            // If you call listen() on a port, then kill the process, then immediately start a new process and 
//            // try to listen() on the same port, you sometimes get WSAEADDRINUSE.  Even if nothing was accepted.  
//            // Ports don't immediately free themselves on process shutdown.  We call listen() in a loop on a delay 
//            // for a few iterations for this reason. 
//            //
//            TimeSpan listenTimeout = TimeSpan.FromSeconds(1);
//            BackoffTimeoutHelper backoffHelper = new BackoffTimeoutHelper(listenTimeout);

//            lock (ThisLock)
//            {
//                if (this.listenSocket != null)
//                {
//                    this.listenSocket.Listen(settings.ListenBacklog);
//                    isListening = true;
//                }

//                while (!isListening)
//                {
//                    try
//                    {
//                        this.listenSocket = new Socket(localEndpoint.AddressFamily, SocketType.Stream, ProtocolType.Tcp);

//                        if (localEndpoint.AddressFamily == AddressFamily.InterNetworkV6 && settings.TeredoEnabled)
//                        {
//                            this.listenSocket.SetSocketOption(SocketOptionLevel.IPv6, (SocketOptionName)23, 10);
//                        }

//                        this.listenSocket.Bind(localEndpoint);
//                        this.listenSocket.Listen(settings.ListenBacklog);
//                        isListening = true;
//                    }
//                    catch (SocketException socketException)
//                    {
//                        bool retry = false;

//                        if (socketException.ErrorCode == UnsafeNativeMethods.WSAEADDRINUSE)
//                        {
//                            if (!backoffHelper.IsExpired())
//                            {
//                                backoffHelper.WaitAndBackoff();
//                                retry = true;
//                            }
//                        }

//                        if (!retry)
//                        {
//                            throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(
//                                SocketConnectionListener.ConvertListenException(socketException, this.localEndpoint));
//                        }
//                    }
//                }

//                this.socketAsyncEventArgsPool = new SocketAsyncEventArgsPool(GetAcceptBufferSize(this.listenSocket));
//            }
//        }

//        public static Exception ConvertListenException(SocketException socketException, IPEndPoint localEndpoint)
//        {
//            if (socketException.ErrorCode == UnsafeNativeMethods.ERROR_INVALID_HANDLE)
//            {
//                return new CommunicationObjectAbortedException(socketException.Message, socketException);
//            }
//            if (socketException.ErrorCode == UnsafeNativeMethods.WSAEADDRINUSE)
//            {
//                return new AddressAlreadyInUseException(SR.GetString(SR.TcpAddressInUse, localEndpoint.ToString()), socketException);
//            }
//            else
//            {
//                return new CommunicationException(
//                    SR.GetString(SR.TcpListenError, socketException.ErrorCode, socketException.Message, localEndpoint.ToString()),
//                    socketException);
//            }
//        }

//        class AcceptAsyncResult : AsyncResult
//        {
//            SocketConnectionListener listener;
//            Socket socket;
//            SocketAsyncEventArgs socketAsyncEventArgs;
//            static Action<object> startAccept;
//            EventTraceActivity eventTraceActivity;

//            // 
//            static EventHandler<SocketAsyncEventArgs> acceptAsyncCompleted = new EventHandler<SocketAsyncEventArgs>(AcceptAsyncCompleted);
//            static Action<AsyncResult, Exception> onCompleting = new Action<AsyncResult, Exception>(OnInternalCompleting);

//            public AcceptAsyncResult(SocketConnectionListener listener, AsyncCallback callback, object state)
//                : base(callback, state)
//            {

//                if (TD.SocketAcceptEnqueuedIsEnabled())
//                {
//                    TD.SocketAcceptEnqueued(this.EventTraceActivity);
//                }

//                Fx.Assert(listener != null, "listener should not be null");
//                this.listener = listener;
//                this.socketAsyncEventArgs = listener.TakeSocketAsyncEventArgs();
//                this.socketAsyncEventArgs.UserToken = this;
//                this.socketAsyncEventArgs.Completed += acceptAsyncCompleted;
//                this.OnCompleting = onCompleting;

//                // If we're going to start up the thread pool eventually anyway, avoid using RegisterWaitForSingleObject
//                if (!Thread.CurrentThread.IsThreadPoolThread)
//                {
//                    if (startAccept == null)
//                    {
//                        startAccept = new Action<object>(StartAccept);
//                    }

//                    ActionItem.Schedule(startAccept, this);
//                }
//                else
//                {
//                    bool completeSelf;
//                    bool success = false;
//                    try
//                    {
//                        completeSelf = StartAccept();
//                        success = true;
//                    }
//                    finally
//                    {
//                        if (!success)
//                        {
//                            // Return the args when an exception is thrown
//                            ReturnSocketAsyncEventArgs();
//                        }
//                    }

//                    if (completeSelf)
//                    {
//                        base.Complete(true);
//                    }
//                }
//            }

//            public EventTraceActivity EventTraceActivity
//            {
//                get
//                {
//                    if (this.eventTraceActivity == null)
//                    {
//                        this.eventTraceActivity = new EventTraceActivity();
//                    }

//                    return this.eventTraceActivity;
//                }
//            }

//            static void StartAccept(object state)
//            {
//                AcceptAsyncResult thisPtr = (AcceptAsyncResult)state;

//                Exception completionException = null;
//                bool completeSelf;
//                try
//                {
//                    completeSelf = thisPtr.StartAccept();
//                }
//#pragma warning suppress 56500 // [....], transferring exception to another thread
//                catch (Exception e)
//                {
//                    if (Fx.IsFatal(e))
//                    {
//                        throw;
//                    }
//                    completeSelf = true;
//                    completionException = e;
//                }
//                if (completeSelf)
//                {
//                    thisPtr.Complete(false, completionException);
//                }
//            }

//            bool StartAccept()
//            {
//                while (true)
//                {
//                    try
//                    {
//                        return listener.InternalBeginAccept(DoAcceptAsync);
//                    }
//                    catch (SocketException socketException)
//                    {
//                        if (ShouldAcceptRecover(socketException))
//                        {
//                            continue;
//                        }
//                        else
//                        {
//                            throw;
//                        }
//                    }
//                }
//            }

//            static bool ShouldAcceptRecover(SocketException exception)
//            {
//                return (
//                    (exception.ErrorCode == UnsafeNativeMethods.WSAECONNRESET) ||
//                    (exception.ErrorCode == UnsafeNativeMethods.WSAEMFILE) ||
//                    (exception.ErrorCode == UnsafeNativeMethods.WSAENOBUFS) ||
//                    (exception.ErrorCode == UnsafeNativeMethods.WSAETIMEDOUT)
//                );
//            }

//            // Return true means completed synchronously
//            bool DoAcceptAsync(Socket listenSocket)
//            {
//                SocketAsyncEventArgsPool.CleanupAcceptSocket(this.socketAsyncEventArgs);

//                if (listenSocket.AcceptAsync(this.socketAsyncEventArgs))
//                {
//                    // AcceptAsync returns true to indicate that the I/O operation is pending (asynchronous)
//                    return false;
//                }

//                Exception exception = HandleAcceptAsyncCompleted();
//                if (exception != null)
//                {
//                    throw DiagnosticUtility.ExceptionUtility.ThrowHelperError(exception);
//                }

//                return true;
//            }

//            static void AcceptAsyncCompleted(object sender, SocketAsyncEventArgs e)
//            {
//                AcceptAsyncResult thisPtr = (AcceptAsyncResult)e.UserToken;
//                Fx.Assert(thisPtr.socketAsyncEventArgs == e, "Got wrong socketAsyncEventArgs");
//                Exception completionException = thisPtr.HandleAcceptAsyncCompleted();
//                if (completionException != null && ShouldAcceptRecover((SocketException)completionException))
//                {
//                    DiagnosticUtility.TraceHandledException(completionException, TraceEventType.Warning);

//                    StartAccept(thisPtr);
//                    return;
//                }

//                thisPtr.Complete(false, completionException);
//            }

//            static void OnInternalCompleting(AsyncResult result, Exception exception)
//            {
//                AcceptAsyncResult thisPtr = result as AcceptAsyncResult;

//                if (TD.SocketAcceptedIsEnabled())
//                {
//                    int hashCode = thisPtr.socket != null ? thisPtr.socket.GetHashCode() : -1;
//                    if (hashCode != -1)
//                    {
//                        TD.SocketAccepted(
//                            thisPtr.EventTraceActivity,
//                            thisPtr.listener != null ? thisPtr.listener.GetHashCode() : -1,
//                            hashCode);
//                    }
//                    else
//                    {
//                        TD.SocketAcceptClosed(thisPtr.EventTraceActivity);
//                    }
//                }

//                Fx.Assert(result != null, "Wrong async result has been passed in to OnInternalCompleting");
//                thisPtr.ReturnSocketAsyncEventArgs();
//            }

//            void ReturnSocketAsyncEventArgs()
//            {
//                if (this.socketAsyncEventArgs != null)
//                {
//                    this.socketAsyncEventArgs.UserToken = null;
//                    this.socketAsyncEventArgs.Completed -= acceptAsyncCompleted;
//                    this.listener.ReturnSocketAsyncEventArgs(this.socketAsyncEventArgs);
//                    this.socketAsyncEventArgs = null;
//                }
//            }

//            Exception HandleAcceptAsyncCompleted()
//            {
//                Exception completionException = null;
//                if (this.socketAsyncEventArgs.SocketError == SocketError.Success)
//                {
//                    this.socket = this.socketAsyncEventArgs.AcceptSocket;
//                    this.socketAsyncEventArgs.AcceptSocket = null;
//                }
//                else
//                {
//                    completionException = new SocketException((int)this.socketAsyncEventArgs.SocketError);
//                }

//                return completionException;
//            }

//            public static Socket End(IAsyncResult result)
//            {
//                AcceptAsyncResult thisPtr = AsyncResult.End<AcceptAsyncResult>(result);
//                return thisPtr.socket;
//            }
//        }
//    }
}
