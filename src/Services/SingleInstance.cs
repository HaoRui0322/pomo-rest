using System;
using System.Threading;

namespace PomoRest.Services
{
    public static class SingleInstance
    {
        public const string MutexName = @"Local\PomoRest.SingleInstance";

        public static bool TryAcquire(out Mutex mutex)
        {
            bool createdNew;
            mutex = new Mutex(true, MutexName, out createdNew);
            return createdNew;
        }

        public static void SignalExisting()
        {
            try
            {
                int msg = NativeMethods.RegisterWindowMessage("PomoRest.ShowWindow");
                NativeMethods.PostMessage(NativeMethods.HWND_BROADCAST, msg, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                // Best effort; the second instance simply exits.
            }
        }
    }
}
