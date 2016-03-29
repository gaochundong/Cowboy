namespace Cowboy.Sockets
{
    public interface IChainableFrameDecoder : IFrameDecoder
    {
        IFrameDecoder NextDecoder { get; set; }
    }
}
