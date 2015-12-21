
namespace Cowboy.Logging
{
    public interface ILogger
    {
        ILog Get(string name);
    }
}
