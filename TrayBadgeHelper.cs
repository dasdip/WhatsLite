using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;

namespace WhatsLite
{
    public static class TrayBadgeHelper
    {
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public static Icon CreateTrayIconWithBadge(Bitmap baseBmp, Icon baseIcon, int count)
        {
            if (count <= 0)
            {
                return baseIcon != null ? (Icon)baseIcon.Clone() : null;
            }

            if (baseBmp == null && baseIcon != null)
            {
                baseBmp = baseIcon.ToBitmap();
            }

            if (baseBmp == null) return null;

            int size = 32;
            using (Bitmap targetBmp = new Bitmap(size, size, PixelFormat.Format32bppArgb))
            using (Graphics g = Graphics.FromImage(targetBmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.InterpolationMode = InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

                // Draw the authentic base WhatsApp icon
                g.DrawImage(baseBmp, new Rectangle(0, 0, size, size));

                // Draw Notification Badge on top-right matching the taskbar overlay style
                float badgeSize = 14f;
                float badgeX = size - badgeSize - 0.5f;
                float badgeY = 0.5f;

                // White outline
                using (SolidBrush whiteBrush = new SolidBrush(Color.White))
                {
                    g.FillEllipse(whiteBrush, badgeX - 1.2f, badgeY - 1.2f, badgeSize + 2.4f, badgeSize + 2.4f);
                }

                // Vibrant Notification Red circle (#E53935)
                using (SolidBrush redBrush = new SolidBrush(Color.FromArgb(229, 57, 53)))
                {
                    g.FillEllipse(redBrush, badgeX, badgeY, badgeSize, badgeSize);
                }

                // Unread number text
                string text = count > 99 ? "99+" : count.ToString();
                float fontSize = text.Length > 2 ? 5.5f : (text.Length > 1 ? 6.5f : 7.5f);

                using (Font font = new Font("Segoe UI", fontSize, FontStyle.Bold, GraphicsUnit.Point))
                using (SolidBrush textBrush = new SolidBrush(Color.White))
                using (StringFormat sf = new StringFormat())
                {
                    sf.Alignment = StringAlignment.Center;
                    sf.LineAlignment = StringAlignment.Center;
                    g.DrawString(text, font, textBrush, new RectangleF(badgeX - 0.5f, badgeY - 0.5f, badgeSize + 1f, badgeSize + 1f), sf);
                }

                IntPtr hIcon = targetBmp.GetHicon();
                try
                {
                    Icon resultIcon = Icon.FromHandle(hIcon);
                    return (Icon)resultIcon.Clone();
                }
                finally
                {
                    DestroyIcon(hIcon);
                }
            }
        }
    }
}
