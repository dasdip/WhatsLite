using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace WhatsLite
{
    [ComImport]
    [Guid("ea1afb91-9e28-4b86-90e9-9e9f8a5eefaf")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    internal interface ITaskbarList3
    {
        // ITaskbarList
        void HrInit();
        void AddTab(IntPtr hWnd);
        void DeleteTab(IntPtr hWnd);
        void ActivateTab(IntPtr hWnd);
        void SetActiveAlt(IntPtr hWnd);

        // ITaskbarList2
        void MarkFullscreenWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool fFullscreen);

        // ITaskbarList3
        void SetProgressValue(IntPtr hWnd, ulong ullCompleted, ulong ullTotal);
        void SetProgressState(IntPtr hWnd, int tbpFlags);
        void RegisterTab(IntPtr hWndTab, IntPtr hWndMDI);
        void UnregisterTab(IntPtr hWndTab);
        void SetTabOrder(IntPtr hWndTab, IntPtr hWndInsertBefore);
        void SetTabActive(IntPtr hWndTab, IntPtr hWndMDI, uint dwReserved);
        void ThumbBarAddButtons(IntPtr hWnd, uint cButtons, IntPtr pButton);
        void ThumbBarUpdateButtons(IntPtr hWnd, uint cButtons, IntPtr pButton);
        void ThumbBarSetImageList(IntPtr hWnd, IntPtr himl);
        void SetOverlayIcon(IntPtr hWnd, IntPtr hIcon, [MarshalAs(UnmanagedType.LPWStr)] string pszDescription);
        void SetThumbnailTooltip(IntPtr hWnd, [MarshalAs(UnmanagedType.LPWStr)] string pszTip);
        void SetThumbnailClip(IntPtr hWnd, IntPtr prcClip);
    }

    [ComImport]
    [Guid("56fdf344-fd6d-11d0-958a-006097c9a090")]
    [ClassInterface(ClassInterfaceType.None)]
    internal class TaskbarInstance
    {
    }

    public static class TaskbarHelper
    {
        private static ITaskbarList3 _taskbarList;
        private static bool _initialized = false;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [StructLayout(LayoutKind.Sequential)]
        private struct FLASHWINFO
        {
            public uint cbSize;
            public IntPtr hwnd;
            public uint dwFlags;
            public uint uCount;
            public uint dwTimeout;
        }

        private const uint FLASHW_STOP = 0;
        private const uint FLASHW_ALL = 3;
        private const uint FLASHW_TIMERNOFG = 12;

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool FlashWindowEx(ref FLASHWINFO pwfi);

        static TaskbarHelper()
        {
            try
            {
                if (Environment.OSVersion.Version.Major >= 6)
                {
                    _taskbarList = (ITaskbarList3)new TaskbarInstance();
                    _taskbarList.HrInit();
                    _initialized = true;
                }
            }
            catch
            {
                _taskbarList = null;
                _initialized = false;
            }
        }

        public static void SetOverlayBadge(IntPtr hWnd, int unreadCount)
        {
            if (!_initialized || _taskbarList == null || hWnd == IntPtr.Zero) return;

            if (unreadCount <= 0)
            {
                _taskbarList.SetOverlayIcon(hWnd, IntPtr.Zero, null);
                return;
            }

            try
            {
                using (Bitmap bmp = GenerateBadgeBitmap(unreadCount))
                {
                    IntPtr hIcon = bmp.GetHicon();
                    try
                    {
                        string desc = string.Format("{0} unread messages", unreadCount);
                        _taskbarList.SetOverlayIcon(hWnd, hIcon, desc);
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
            }
            catch {}
        }

        public static void ClearOverlayBadge(IntPtr hWnd)
        {
            if (!_initialized || _taskbarList == null || hWnd == IntPtr.Zero) return;
            try
            {
                _taskbarList.SetOverlayIcon(hWnd, IntPtr.Zero, null);
            }
            catch {}
        }

        public static void FlashWindow(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                FLASHWINFO fi = new FLASHWINFO();
                fi.cbSize = (uint)Marshal.SizeOf(fi);
                fi.hwnd = hWnd;
                fi.dwFlags = FLASHW_ALL | FLASHW_TIMERNOFG;
                fi.uCount = 0;
                fi.dwTimeout = 0;
                FlashWindowEx(ref fi);
            }
            catch {}
        }

        public static void StopFlashing(IntPtr hWnd)
        {
            if (hWnd == IntPtr.Zero) return;
            try
            {
                FLASHWINFO fi = new FLASHWINFO();
                fi.cbSize = (uint)Marshal.SizeOf(fi);
                fi.hwnd = hWnd;
                fi.dwFlags = FLASHW_STOP;
                fi.uCount = 0;
                fi.dwTimeout = 0;
                FlashWindowEx(ref fi);
            }
            catch {}
        }

        public static Bitmap GenerateBadgeBitmap(int count)
        {
            int size = 16;
            Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // Notification badge: #E53935
                using (SolidBrush redBrush = new SolidBrush(Color.FromArgb(229, 57, 53)))
                {
                    g.FillEllipse(redBrush, 0, 0, size - 1, size - 1);
                }

                // White border
                using (Pen borderPen = new Pen(Color.White, 1.2f))
                {
                    g.DrawEllipse(borderPen, 0.6f, 0.6f, size - 2.2f, size - 2.2f);
                }

                string text = count > 99 ? "99+" : count.ToString();
                float fontSize = text.Length > 2 ? 6.0f : (text.Length > 1 ? 7.0f : 8.0f);

                using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(text, font, textBrush, new RectangleF(-0.5f, -0.5f, size + 1f, size + 1f), sf);
                }
            }
            return bmp;
        }
    }
}
