using System.Threading.Tasks;

namespace Cowboy.Sockets
{
    public static class TplExtensions
    {
        public static void Forget(this Task task) { }
    }
}