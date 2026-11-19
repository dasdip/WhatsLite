using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

namespace WhatsLite
{
    static class Program
    {
        private const string MutexId = @"Local\WhatsLite_SingleInstance_App_Mutex_98a72b1c";
        private const string EventId = @"Local\WhatsLite_SingleInstance_Show_Event_98a72b1c";

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        private const int ASFW_ANY = -1;
        private static readonly IntPtr HWND_BROADCAST = (IntPtr)0xffff;

        [STAThread]
        static void Main(string[] args)
        {
            bool isNewInstance;
            using (Mutex mutex = new Mutex(true, MutexId, out isNewInstance))
            {
                if (!isNewInstance)
                {
                    // An instance is already running!
                    try
                    {
                        // Allow running instance to become foreground
                        AllowSetForegroundWindow(ASFW_ANY);

                        // Signal existing instance via kernel EventWaitHandle
                        using (EventWaitHandle showEvent = EventWaitHandle.OpenExisting(EventId))
                        {
                            showEvent.Set();
                        }
                    }
                    catch {}

                    // Fallback broadcast message
                    try
                    {
                        PostMessage(HWND_BROADCAST, MainForm.WM_SHOW_WHATSLITE, IntPtr.Zero, IntPtr.Zero);
                    }
                    catch {}

                    return;
                }

                bool startHidden = false;
                foreach (string arg in args)
                {
                    if (arg.Equals("--tray", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("--minimized", StringComparison.OrdinalIgnoreCase) ||
                        arg.Equals("/tray", StringComparison.OrdinalIgnoreCase))
                    {
                        startHidden = true;
                        break;
                    }
                }

                // First instance: create the EventWaitHandle to listen for secondary launches
                EventWaitHandle eventHandle = new EventWaitHandle(false, EventResetMode.AutoReset, EventId);

                // Listen for signals on a dedicated thread pool thread
                ThreadPool.QueueUserWorkItem(delegate {
                    while (true)
                    {
                        try
                        {
                            eventHandle.WaitOne();
                            if (MainForm.Instance != null && !MainForm.Instance.IsDisposed)
                            {
                                MainForm.Instance.BeginInvoke(new Action(delegate {
                                    MainForm.Instance.ShowFromTray();
                                }));
                            }
                        }
                        catch {}
                    }
                });

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm(startHidden));
            }
        }
    }
}
