// The MIT License (MIT)
// 
// Copyright (c) 2015 Allan Lindqvist
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in all
// copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
// SOFTWARE.

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
