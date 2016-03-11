using System.Threading.Tasks;

namespace Cowboy.Buffer.ByteBuffers
{
    public static class TaskEx
    {
        public static readonly Task<int> Zero = Task.FromResult(0);

        public static readonly Task<int> Completed = Zero;
    }
}
