using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("WhatsLite Uninstaller")]
[assembly: AssemblyDescription("WhatsLite 1.3 Uninstaller")]
[assembly: AssemblyProduct("WhatsLite")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3")]

namespace WhatsLite.Installer
{
    static class UninstallProgram
    {
        [STAThread]
        static void Main(string[] args)
        {
            bool isSilent = false;
            foreach (string arg in args)
            {
                if (arg.Equals("/S", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("--silent", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("/silent", StringComparison.OrdinalIgnoreCase))
                {
                    isSilent = true;
                    break;
                }
            }

            if (!isSilent)
            {
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);

                DialogResult dr = MessageBox.Show(
                    "Are you sure you want to completely uninstall WhatsLite from your computer?",
                    "Uninstall WhatsLite",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question
                );

                if (dr != DialogResult.Yes)
                {
                    return;
                }
            }

            try
            {
                // 1. Close any running WhatsLite processes
                foreach (Process p in Process.GetProcesses())
                {
                    try
                    {
                        if (p.ProcessName.IndexOf("WhatsLite", StringComparison.OrdinalIgnoreCase) >= 0 &&
                            p.Id != Process.GetCurrentProcess().Id)
                        {
                            p.Kill();
                            p.WaitForExit(2000);
                        }
                    }
                    catch {}
                    finally { p.Dispose(); }
                }

                // 2. Remove Shortcuts
                string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WhatsLite.lnk");
                if (File.Exists(desktopLnk))
                {
                    try { File.Delete(desktopLnk); } catch {}
                }

                string startMenuLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "WhatsLite.lnk");
                if (File.Exists(startMenuLnk))
                {
                    try { File.Delete(startMenuLnk); } catch {}
                }

                // 3. Remove Startup Registry
                try
                {
                    using (RegistryKey runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                    {
                        if (runKey != null) runKey.DeleteValue("WhatsLite", false);
                    }
                }
                catch {}

                // 4. Remove Add/Remove Programs Registry
                try
                {
                    Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\WhatsLite", false);
                }
                catch {}

                // 5. Spawn background command to delete install folder after this process exits
                string installDir = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = "cmd.exe";
                psi.Arguments = string.Format("/c ping 127.0.0.1 -n 2 > nul & rd /s /q \"{0}\"", installDir);
                psi.WindowStyle = ProcessWindowStyle.Hidden;
                psi.CreateNoWindow = true;
                psi.UseShellExecute = true;
                Process.Start(psi);

                if (!isSilent)
                {
                    MessageBox.Show(
                        "WhatsLite has been successfully uninstalled from your computer.",
                        "WhatsLite Uninstalled",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information
                    );
                }
            }
            catch (Exception ex)
            {
                if (!isSilent)
                {
                    MessageBox.Show("Uninstall error: " + ex.Message, "WhatsLite", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
