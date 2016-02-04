using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;

namespace Cowboy.Sockets.Experimental
{
    internal static class Kernel32
    {
        const string Kernel_32 = "Kernel32";
        internal static IntPtr INVALID_HANDLE_VALUE = new IntPtr(-1);

        [DllImport(Kernel_32, SetLastError = true)]
        internal unsafe static extern IntPtr CreateIoCompletionPort(IntPtr handle, IntPtr hExistingCompletionPort, int puiCompletionKey, uint uiNumberOfConcurrentThreads);

        [DllImport(Kernel_32, SetLastError = true, EntryPoint = "GetQueuedCompletionStatus")]
        internal static extern unsafe bool GetQueuedCompletionStatusRio(IntPtr CompletionPort, out IntPtr lpNumberOfBytes, out IntPtr lpCompletionKey, out RioNativeOverlapped* lpOverlapped, int dwMilliseconds);

        [DllImport(Kernel_32, SetLastError = true)]
        internal static extern unsafe int GetQueuedCompletionStatus(IntPtr CompletionPort, out IntPtr lpNumberOfBytes, out IntPtr lpCompletionKey, out NativeOverlapped* lpOverlapped, int dwMilliseconds);

        internal static int ThrowLastError()
        {
            var error = Marshal.GetLastWin32Error();

            if (error != 0)
                throw new Win32Exception(error);
            else
                return error;
        }

        [DllImport(Kernel_32, SetLastError = true)]
        internal static extern IntPtr CreateEvent([In, Optional]IntPtr lpEventAttributes, [In]bool bManualReset, [In]bool bInitialState, [In, Optional]string lpName);

        [DllImport(Kernel_32, SetLastError = true)]
        internal static extern IntPtr ResetEvent([In]IntPtr handle);

        [DllImport(Kernel_32, SetLastError = true)]
        internal static extern IntPtr CloseHandle([In]IntPtr handle);
    }

    internal static class Msvcrt
    {
        [DllImport("msvcrt.dll", EntryPoint = "memset", CallingConvention = CallingConvention.Cdecl, SetLastError = false)]
        internal static extern IntPtr MemSet(IntPtr dest, int c, int count);
    }
}
