using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    internal sealed class SaeaPool
    {
        private ConcurrentStack<SocketAsyncEventArgs> _pool;
        private int _nextUserToken = 100000;

        public SaeaPool()
        {
            _pool = new ConcurrentStack<SocketAsyncEventArgs>();
        }

        public int Count
        {
            get { return _pool.Count; }
        }

        public bool IsEmpty
        {
            get { return _pool.IsEmpty; }
        }

        public bool TryPop(out SocketAsyncEventArgs args)
        {
            return _pool.TryPop(out args);
        }

        public void Push(SocketAsyncEventArgs args)
        {
            if (args == null)
                throw new ArgumentNullException("args");

            _pool.Push(args);
        }

        private int AssignUserToken()
        {
            return Interlocked.Increment(ref _nextUserToken);
        }

        private SocketAsyncEventArgs CreateNewSaeaForAccept()
        {
            var args = new SocketAsyncEventArgs();

            args.Completed += OnCompleted;
            args.UserToken = AssignUserToken();

            return args;
        }

        private void OnCompleted(object sender, SocketAsyncEventArgs e)
        {
            ProcessAccept(e);
        }

        //____________________________________________________________________________       
        //The e parameter passed from the AcceptEventArg_Completed method
        //represents the SocketAsyncEventArgs object that did
        //the accept operation. in this method we'll do the handoff from it to the 
        //SocketAsyncEventArgs object that will do receive/send.
        private void ProcessAccept(SocketAsyncEventArgs acceptEventArgs)
        {
            // This is when there was an error with the accept op. That should NOT
            // be happening often. It could indicate that there is a problem with
            // that socket. If there is a problem, then we would have an infinite
            // loop here, if we tried to reuse that same socket.
            if (acceptEventArgs.SocketError != SocketError.Success)
            {
                // Loop back to post another accept op. Notice that we are NOT
                // passing the SAEA object here.
                LoopToStartAccept();

                AcceptOpUserToken theAcceptOpToken = (AcceptOpUserToken)acceptEventArgs.UserToken;
                Program.testWriter.WriteLine("SocketError, accept id " + theAcceptOpToken.TokenId);

                //Let's destroy this socket, since it could be bad.
                HandleBadAccept(acceptEventArgs);

                //Jump out of the method.
                return;
            }

            Int32 max = Program.maxSimultaneousClientsThatWereConnected;
            Int32 numberOfConnectedSockets = Interlocked.Increment(ref this.numberOfAcceptedSockets);
            if (numberOfConnectedSockets > max)
            {
                Interlocked.Increment(ref Program.maxSimultaneousClientsThatWereConnected);
            }

            if (Program.watchProgramFlow == true)   //for testing
            {
                AcceptOpUserToken theAcceptOpToken = (AcceptOpUserToken)acceptEventArgs.UserToken;
                Program.testWriter.WriteLine("ProcessAccept, accept id " + theAcceptOpToken.TokenId);
            }



            //Now that the accept operation completed, we can start another
            //accept operation, which will do the same. Notice that we are NOT
            //passing the SAEA object here.
            LoopToStartAccept();

            // Get a SocketAsyncEventArgs object from the pool of receive/send op 
            //SocketAsyncEventArgs objects
            SocketAsyncEventArgs receiveSendEventArgs = this.poolOfRecSendEventArgs.Pop();

            //Create sessionId in UserToken.
            ((DataHoldingUserToken)receiveSendEventArgs.UserToken).CreateSessionId();

            //A new socket was created by the AcceptAsync method. The 
            //SocketAsyncEventArgs object which did the accept operation has that 
            //socket info in its AcceptSocket property. Now we will give
            //a reference for that socket to the SocketAsyncEventArgs 
            //object which will do receive/send.
            receiveSendEventArgs.AcceptSocket = acceptEventArgs.AcceptSocket;

            if ((Program.watchProgramFlow == true) || (Program.watchConnectAndDisconnect == true))
            {
                AcceptOpUserToken theAcceptOpToken = (AcceptOpUserToken)acceptEventArgs.UserToken;
                Program.testWriter.WriteLine("Accept id " + theAcceptOpToken.TokenId + ". RecSend id " + ((DataHoldingUserToken)receiveSendEventArgs.UserToken).TokenId + ".  Remote endpoint = " + IPAddress.Parse(((IPEndPoint)receiveSendEventArgs.AcceptSocket.RemoteEndPoint).Address.ToString()) + ": " + ((IPEndPoint)receiveSendEventArgs.AcceptSocket.RemoteEndPoint).Port.ToString() + ". client(s) connected = " + this.numberOfAcceptedSockets);
            }
            if (Program.watchThreads == true)   //for testing
            {
                AcceptOpUserToken theAcceptOpToken = (AcceptOpUserToken)acceptEventArgs.UserToken;
                theAcceptOpToken.socketHandleNumber = (Int32)acceptEventArgs.AcceptSocket.Handle;
                DealWithThreadsForTesting("ProcessAccept()", theAcceptOpToken);
                ((DataHoldingUserToken)receiveSendEventArgs.UserToken).socketHandleNumber = (Int32)receiveSendEventArgs.AcceptSocket.Handle;
            }

            //We have handed off the connection info from the
            //accepting socket to the receiving socket. So, now we can
            //put the SocketAsyncEventArgs object that did the accept operation 
            //back in the pool for them. But first we will clear 
            //the socket info from that object, so it will be 
            //ready for a new socket when it comes out of the pool.
            acceptEventArgs.AcceptSocket = null;
            this.poolOfAcceptEventArgs.Push(acceptEventArgs);

            if (Program.watchProgramFlow == true)   //for testing
            {
                AcceptOpUserToken theAcceptOpToken = (AcceptOpUserToken)acceptEventArgs.UserToken;
                Program.testWriter.WriteLine("back to poolOfAcceptEventArgs goes accept id " + theAcceptOpToken.TokenId);
            }

            StartReceive(receiveSendEventArgs);
        }
    }
}
