using System;
using System.Drawing;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace WhatsLite
{
    public class WindowSettings
    {
        public int X = 100;
        public int Y = 20;
        public int Width = 1320;
        public int Height = 780;
        public bool IsMaximized = false;
        public bool StartWithWindows = false;
        public bool LowMemoryMode = true;

        private static string SettingsFilePath
        {
            get
            {
                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "WhatsLite");
                if (!Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }
                return Path.Combine(dir, "settings.json");
            }
        }

        public static WindowSettings Load()
        {
            WindowSettings s = new WindowSettings();

            // Set optimal default dimensions (1320 x 780 centered)
            try
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                s.Width = Math.Min(1320, Math.Max(1000, (int)(wa.Width * 0.88)));
                s.Height = Math.Min(780, Math.Max(650, (int)(wa.Height * 0.94)));
                s.X = wa.X + (wa.Width - s.Width) / 2;
                s.Y = wa.Y + (wa.Height - s.Height) / 2;
            }
            catch {}

            try
            {
                string file = SettingsFilePath;
                if (File.Exists(file))
                {
                    string json = File.ReadAllText(file);
                    int savedW = GetInt(json, "width", 0);
                    int savedH = GetInt(json, "height", 0);

                    // Ensure minimum width of 1280 so WhatsApp Web's right-side chat menu is never hidden
                    if (savedW >= 1280)
                    {
                        s.Width = savedW;
                        if (savedH >= 600) s.Height = savedH;
                        s.X = GetInt(json, "x", s.X);
                        s.Y = GetInt(json, "y", s.Y);
                    }

                    s.IsMaximized = GetBool(json, "isMaximized", s.IsMaximized);
                    s.StartWithWindows = GetBool(json, "startWithWindows", s.StartWithWindows);
                    s.LowMemoryMode = GetBool(json, "lowMemoryMode", s.LowMemoryMode);
                }
            }
            catch {}
            return s;
        }

        public void Save()
        {
            try
            {
                string json = string.Format(
                    "{{\n  \"x\": {0},\n  \"y\": {1},\n  \"width\": {2},\n  \"height\": {3},\n  \"isMaximized\": {4},\n  \"startWithWindows\": {5},\n  \"lowMemoryMode\": {6}\n}}",
                    X, Y, Width, Height,
                    IsMaximized ? "true" : "false",
                    StartWithWindows ? "true" : "false",
                    LowMemoryMode ? "true" : "false"
                );
                File.WriteAllText(SettingsFilePath, json);
            }
            catch {}
        }

        private static int GetInt(string json, string key, int defaultVal)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(-?\\d+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                int val;
                if (int.TryParse(m.Groups[1].Value, out val)) return val;
            }
            return defaultVal;
        }

        private static bool GetBool(string json, string key, bool defaultVal)
        {
            Match m = Regex.Match(json, "\"" + key + "\"\\s*:\\s*(true|false)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                return m.Groups[1].Value.Equals("true", StringComparison.OrdinalIgnoreCase);
            }
            return defaultVal;
        }
    }
}
