using System;

namespace Cowboy.Sockets
{
    public interface ISession
    {
        Guid SessionId { get; }
        string SessionKey { get; }
        bool Connected { get; }
    }
}
