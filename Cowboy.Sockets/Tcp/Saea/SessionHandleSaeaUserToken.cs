using System.Threading;

namespace Cowboy.Sockets
{
    internal class SessionHandleSaeaUserToken
    {
        private static int _nextTokenId = 0;

        public SessionHandleSaeaUserToken()
        {
            var tokenId = Interlocked.Increment(ref _nextTokenId);
            this.TokenId = tokenId;
        }

        public int TokenId { get; private set; }
    }
}
