namespace Cowboy.Sockets
{
    public interface IChainableFrameEncoder : IFrameEncoder
    {
        IFrameEncoder NextEncoder { get; set; }
    }
}
