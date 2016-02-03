using System.Threading;

namespace Cowboy.Sockets
{
    internal class SessionAcceptSaeaUserToken
    {
        private static int _nextTokenId = 0;

        public SessionAcceptSaeaUserToken()
        {
            var tokenId = Interlocked.Increment(ref _nextTokenId);
            this.TokenId = tokenId;
        }

        public int TokenId { get; private set; }
    }
}
