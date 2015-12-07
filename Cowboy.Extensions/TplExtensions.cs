using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Cowboy.Extensions
{
    public static class TplExtensions
    {
        public static readonly Task<bool> TrueTask = Task.FromResult<bool>(true);
        public static readonly Task<bool> FalseTask = Task.FromResult<bool>(false);

        public static void Forget(this Task task)
        {
        }
    }
}
