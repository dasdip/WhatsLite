using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WhatsLite
{
    public static class MemoryOptimizer
    {
        [DllImport("psapi.dll")]
        private static extern int EmptyWorkingSet(IntPtr hwProc);

        [DllImport("kernel32.dll")]
        private static extern bool SetProcessWorkingSetSize(IntPtr proc, int min, int max);

        public static void TrimMemory()
        {
            try
            {
                // Force garbage collection
                GC.Collect(2, GCCollectionMode.Forced);
                GC.WaitForPendingFinalizers();
                GC.Collect(2, GCCollectionMode.Forced);

                // Trim main process working set
                Process current = Process.GetCurrentProcess();
                EmptyWorkingSet(current.Handle);
                SetProcessWorkingSetSize(current.Handle, -1, -1);

                // Trim child WebView2 processes
                Process[] procs = Process.GetProcessesByName("msedgewebview2");
                foreach (Process p in procs)
                {
                    try
                    {
                        EmptyWorkingSet(p.Handle);
                    }
                    catch {}
                    finally
                    {
                        p.Dispose();
                    }
                }
            }
            catch {}
        }
    }
}
