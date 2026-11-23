using System;
using System.Drawing;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WhatsLite
{
    public static class ThemeHelper
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        private const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        /// <summary>
        /// Detects if Windows Apps color mode is set to Dark Mode (AppsUseLightTheme == 0).
        /// Returns true if Windows is Dark Mode, false if Light Mode.
        /// </summary>
        public static bool IsWindowsDarkTheme()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))
                {
                    if (key != null)
                    {
                        object val = key.GetValue("AppsUseLightTheme");
                        if (val is int)
                        {
                            return ((int)val) == 0;
                        }
                    }
                }
            }
            catch {}
            return false;
        }

        /// <summary>
        /// Applies immersive dark or light mode to the native Windows title bar / window header.
        /// If isDark is true, makes the title bar dark; if false, makes it light/white as per Windows mode.
        /// </summary>
        public static void ApplyTitleBarTheme(IntPtr hWnd, bool isDark)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                int useDark = isDark ? 1 : 0;
                int hr = DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE, ref useDark, sizeof(int));
                if (hr != 0)
                {
                    DwmSetWindowAttribute(hWnd, DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1, ref useDark, sizeof(int));
                }
            }
            catch {}
        }
    }
}
