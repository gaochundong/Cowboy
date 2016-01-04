using System.Globalization;
using System.Runtime.InteropServices;
using System.Threading;

namespace Cowboy.TcpLika
{
    internal static class ThreadExtensions
    {
        public static uint GetUnmanagedThreadId(this Thread context)
        {
            return GetCurrentThreadId();
        }

        public static string GetDescription(this Thread context)
        {
            return string.Format(CultureInfo.InvariantCulture, "ManagedThreadId[{0}], UnmanagedThreadId[{1}]",
              context.ManagedThreadId,
              context.GetUnmanagedThreadId());
        }

        [DllImport("kernel32.dll")]
        internal static extern uint GetCurrentThreadId();
    }
}
