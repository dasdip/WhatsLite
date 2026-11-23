using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;

[assembly: AssemblyTitle("WhatsLite Setup")]
[assembly: AssemblyDescription("WhatsLite 1.3 Installer")]
[assembly: AssemblyProduct("WhatsLite")]
[assembly: AssemblyCompany("WhatsLite")]
[assembly: AssemblyVersion("1.3.0.0")]
[assembly: AssemblyFileVersion("1.3.0.0")]
[assembly: AssemblyInformationalVersion("1.3")]

namespace WhatsLite.Installer
{
    static class Program
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

            if (isSilent)
            {
                // Perform silent installation to default path
                string defaultDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Programs", "WhatsLite");
                InstallerEngine.Install(defaultDir, true, true, false, null, null);
                return;
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new SetupWizardForm());
        }
    }

    public static class InstallerEngine
    {
        public static readonly string[] PayloadFiles = new string[] {
            "WhatsLite.exe",
            "Microsoft.Web.WebView2.Core.dll",
            "Microsoft.Web.WebView2.WinForms.dll",
            "WebView2Loader.dll",
            "app.ico",
            "Uninstall.exe"
        };

        public static void Install(
            string targetDir,
            bool createDesktopShortcut,
            bool createStartMenuShortcut,
            bool launchAfter,
            Action<int, string> onProgress,
            Action<bool, string> onComplete)
        {
            try
            {
                // 1. Close running instances
                if (onProgress != null) onProgress(5, "Closing running instances...");
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

                // 2. Prepare target directory
                if (onProgress != null) onProgress(15, "Creating installation directory...");
                if (!Directory.Exists(targetDir))
                {
                    Directory.CreateDirectory(targetDir);
                }

                // 3. Extract and decompress all embedded payload files
                Assembly asm = Assembly.GetExecutingAssembly();
                int fileIndex = 0;
                foreach (string fileName in PayloadFiles)
                {
                    fileIndex++;
                    int pct = 20 + (int)(55.0 * fileIndex / PayloadFiles.Length);
                    if (onProgress != null) onProgress(pct, string.Format("Extracting {0}...", fileName));

                    string resourceName = fileName + ".gz";
                    using (Stream resStream = asm.GetManifestResourceStream(resourceName))
                    {
                        if (resStream == null)
                        {
                            throw new Exception("Embedded resource not found: " + resourceName);
                        }

                        string destFile = Path.Combine(targetDir, fileName);
                        using (GZipStream gz = new GZipStream(resStream, CompressionMode.Decompress))
                        using (FileStream outFs = new FileStream(destFile, FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[65536];
                            int read;
                            while ((read = gz.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                outFs.Write(buffer, 0, read);
                            }
                        }
                    }
                }

                string exePath = Path.Combine(targetDir, "WhatsLite.exe");
                string icoPath = Path.Combine(targetDir, "app.ico");
                string uninstallExe = Path.Combine(targetDir, "Uninstall.exe");

                // 4. Create Shortcuts
                if (onProgress != null) onProgress(80, "Creating shortcuts...");
                if (createDesktopShortcut)
                {
                    string desktopLnk = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "WhatsLite.lnk");
                    CreateShortcut(desktopLnk, exePath, targetDir, icoPath, "WhatsLite Desktop Client");
                }

                if (createStartMenuShortcut)
                {
                    string startMenuDir = Environment.GetFolderPath(Environment.SpecialFolder.Programs);
                    string startMenuLnk = Path.Combine(startMenuDir, "WhatsLite.lnk");
                    CreateShortcut(startMenuLnk, exePath, targetDir, icoPath, "WhatsLite Desktop Client");
                }

                // 5. Register in Windows Add/Remove Programs (Settings > Apps)
                if (onProgress != null) onProgress(90, "Registering application...");
                RegisterUninstall(targetDir, exePath, icoPath, uninstallExe);

                if (onProgress != null) onProgress(100, "Installation complete!");

                if (launchAfter && File.Exists(exePath))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo(exePath) { WorkingDirectory = targetDir });
                    }
                    catch {}
                }

                if (onComplete != null) onComplete(true, null);
            }
            catch (Exception ex)
            {
                if (onComplete != null) onComplete(false, ex.Message);
            }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string iconPath, string description)
        {
            try
            {
                Type shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    object shell = Activator.CreateInstance(shellType);
                    object shortcut = shellType.InvokeMember("CreateShortcut", BindingFlags.InvokeMethod, null, shell, new object[] { shortcutPath });
                    Type scType = shortcut.GetType();

                    scType.InvokeMember("TargetPath", BindingFlags.SetProperty, null, shortcut, new object[] { targetPath });
                    scType.InvokeMember("WorkingDirectory", BindingFlags.SetProperty, null, shortcut, new object[] { workingDir });
                    scType.InvokeMember("Description", BindingFlags.SetProperty, null, shortcut, new object[] { description });
                    scType.InvokeMember("IconLocation", BindingFlags.SetProperty, null, shortcut, new object[] { iconPath + ",0" });
                    scType.InvokeMember("Save", BindingFlags.InvokeMethod, null, shortcut, null);
                }
            }
            catch {}
        }

        private static void RegisterUninstall(string installDir, string exePath, string icoPath, string uninstallExe)
        {
            try
            {
                string keyPath = @"Software\Microsoft\Windows\CurrentVersion\Uninstall\WhatsLite";
                using (RegistryKey key = Registry.CurrentUser.CreateSubKey(keyPath))
                {
                    if (key != null)
                    {
                        key.SetValue("DisplayName", "WhatsLite");
                        key.SetValue("DisplayVersion", "1.3");
                        key.SetValue("Publisher", "Mr. Dipendu Das");
                        key.SetValue("DisplayIcon", icoPath);
                        key.SetValue("UninstallString", "\"" + uninstallExe + "\"");
                        key.SetValue("InstallLocation", installDir);
                        key.SetValue("EstimatedSize", 1200, RegistryValueKind.DWord);
                        key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                        key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                    }
                }
            }
            catch {}
        }
    }

    public class SetupWizardForm : Form
    {
        private Panel _headerPanel;
        private Label _lblHeaderTitle;
        private Label _lblHeaderSubtitle;
        private PictureBox _picHeaderIcon;

        private Label _lblDest;
        private TextBox _txtDest;
        private Button _btnBrowse;

        private CheckBox _chkDesktop;
        private CheckBox _chkStartMenu;
        private CheckBox _chkLaunch;

        private ProgressBar _progressBar;
        private Label _lblStatus;

        private Button _btnInstall;
        private Button _btnCancel;

        private bool _isInstalled = false;

        public SetupWizardForm()
        {
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = "WhatsLite 1.3 Setup";
            this.StartPosition = FormStartPosition.CenterScreen;
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.ClientSize = new Size(540, 390);
            this.BackColor = Color.FromArgb(248, 249, 250);

            // Set Form Icon from embedded resource or assembly
            try
            {
                Stream icoStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("app.ico.gz");
                if (icoStream != null)
                {
                    using (GZipStream gz = new GZipStream(icoStream, CompressionMode.Decompress))
                    using (MemoryStream ms = new MemoryStream())
                    {
                        gz.CopyTo(ms);
                        ms.Position = 0;
                        this.Icon = new Icon(ms);
                    }
                }
            }
            catch {}

            // Header Banner
            _headerPanel = new Panel();
            _headerPanel.Dock = DockStyle.Top;
            _headerPanel.Height = 85;
            _headerPanel.BackColor = Color.FromArgb(18, 140, 126); // WhatsApp Teal

            _lblHeaderTitle = new Label();
            _lblHeaderTitle.Text = "WhatsLite 1.3 Setup Wizard";
            _lblHeaderTitle.Font = new Font("Segoe UI", 13.5f, FontStyle.Bold);
            _lblHeaderTitle.ForeColor = Color.White;
            _lblHeaderTitle.Location = new Point(24, 18);
            _lblHeaderTitle.AutoSize = true;

            _lblHeaderSubtitle = new Label();
            _lblHeaderSubtitle.Text = "Install the lightweight Windows desktop client for WhatsApp";
            _lblHeaderSubtitle.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            _lblHeaderSubtitle.ForeColor = Color.FromArgb(220, 245, 240);
            _lblHeaderSubtitle.Location = new Point(25, 48);
            _lblHeaderSubtitle.AutoSize = true;

            _headerPanel.Controls.Add(_lblHeaderTitle);
            _headerPanel.Controls.Add(_lblHeaderSubtitle);
            this.Controls.Add(_headerPanel);

            // Destination Folder controls
            _lblDest = new Label();
            _lblDest.Text = "Installation Folder:";
            _lblDest.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            _lblDest.ForeColor = Color.FromArgb(33, 37, 41);
            _lblDest.Location = new Point(28, 105);
            _lblDest.AutoSize = true;

            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Programs",
                "WhatsLite"
            );

            _txtDest = new TextBox();
            _txtDest.Text = defaultPath;
            _txtDest.Font = new Font("Segoe UI", 9.0f);
            _txtDest.Location = new Point(28, 130);
            _txtDest.Size = new Size(385, 25);

            _btnBrowse = new Button();
            _btnBrowse.Text = "Browse...";
            _btnBrowse.Font = new Font("Segoe UI", 9.0f);
            _btnBrowse.Location = new Point(420, 128);
            _btnBrowse.Size = new Size(88, 28);
            _btnBrowse.Click += BtnBrowse_Click;

            // Options Checkboxes
            _chkDesktop = new CheckBox();
            _chkDesktop.Text = "Create a Desktop shortcut";
            _chkDesktop.Font = new Font("Segoe UI", 9.5f);
            _chkDesktop.ForeColor = Color.FromArgb(33, 37, 41);
            _chkDesktop.Location = new Point(28, 175);
            _chkDesktop.Size = new Size(300, 24);
            _chkDesktop.Checked = true;

            _chkStartMenu = new CheckBox();
            _chkStartMenu.Text = "Create a Start Menu shortcut";
            _chkStartMenu.Font = new Font("Segoe UI", 9.5f);
            _chkStartMenu.ForeColor = Color.FromArgb(33, 37, 41);
            _chkStartMenu.Location = new Point(28, 205);
            _chkStartMenu.Size = new Size(300, 24);
            _chkStartMenu.Checked = true;

            _chkLaunch = new CheckBox();
            _chkLaunch.Text = "Launch WhatsLite after installation";
            _chkLaunch.Font = new Font("Segoe UI", 9.5f);
            _chkLaunch.ForeColor = Color.FromArgb(33, 37, 41);
            _chkLaunch.Location = new Point(28, 235);
            _chkLaunch.Size = new Size(300, 24);
            _chkLaunch.Checked = true;

            // Progress controls
            _progressBar = new ProgressBar();
            _progressBar.Location = new Point(28, 280);
            _progressBar.Size = new Size(480, 20);
            _progressBar.Visible = false;

            _lblStatus = new Label();
            _lblStatus.Text = "Click Install to begin.";
            _lblStatus.Font = new Font("Segoe UI", 8.5f, FontStyle.Italic);
            _lblStatus.ForeColor = Color.FromArgb(108, 117, 125);
            _lblStatus.Location = new Point(28, 305);
            _lblStatus.Size = new Size(480, 20);

            // Bottom Buttons
            _btnInstall = new Button();
            _btnInstall.Text = "Install";
            _btnInstall.Font = new Font("Segoe UI", 9.5f, FontStyle.Bold);
            _btnInstall.BackColor = Color.FromArgb(37, 211, 102); // WhatsApp Vibrant Green
            _btnInstall.ForeColor = Color.White;
            _btnInstall.FlatStyle = FlatStyle.Flat;
            _btnInstall.FlatAppearance.BorderSize = 0;
            _btnInstall.Location = new Point(310, 335);
            _btnInstall.Size = new Size(100, 34);
            _btnInstall.Click += BtnInstall_Click;

            _btnCancel = new Button();
            _btnCancel.Text = "Cancel";
            _btnCancel.Font = new Font("Segoe UI", 9.0f);
            _btnCancel.Location = new Point(420, 335);
            _btnCancel.Size = new Size(88, 34);
            _btnCancel.Click += delegate { this.Close(); };

            this.Controls.Add(_lblDest);
            this.Controls.Add(_txtDest);
            this.Controls.Add(_btnBrowse);
            this.Controls.Add(_chkDesktop);
            this.Controls.Add(_chkStartMenu);
            this.Controls.Add(_chkLaunch);
            this.Controls.Add(_progressBar);
            this.Controls.Add(_lblStatus);
            this.Controls.Add(_btnInstall);
            this.Controls.Add(_btnCancel);

            this.ResumeLayout(false);
        }

        private void BtnBrowse_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog fbd = new FolderBrowserDialog())
            {
                fbd.Description = "Select Destination Folder for WhatsLite";
                fbd.SelectedPath = _txtDest.Text;
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    _txtDest.Text = fbd.SelectedPath;
                }
            }
        }

        private void BtnInstall_Click(object sender, EventArgs e)
        {
            if (_isInstalled)
            {
                // Finished
                this.Close();
                return;
            }

            string targetDir = _txtDest.Text.Trim();
            if (string.IsNullOrEmpty(targetDir))
            {
                MessageBox.Show("Please specify a valid installation directory.", "WhatsLite Setup", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Disable controls during installation
            _txtDest.Enabled = false;
            _btnBrowse.Enabled = false;
            _chkDesktop.Enabled = false;
            _chkStartMenu.Enabled = false;
            _chkLaunch.Enabled = false;
            _btnInstall.Enabled = false;
            _btnCancel.Enabled = false;

            _progressBar.Visible = true;
            _progressBar.Value = 0;

            bool desktop = _chkDesktop.Checked;
            bool startMenu = _chkStartMenu.Checked;
            bool launch = _chkLaunch.Checked;

            Task.Factory.StartNew(delegate {
                InstallerEngine.Install(
                    targetDir,
                    desktop,
                    startMenu,
                    launch,
                    delegate(int pct, string status) {
                        this.BeginInvoke(new Action(delegate {
                            _progressBar.Value = Math.Min(100, Math.Max(0, pct));
                            _lblStatus.Text = status;
                        }));
                    },
                    delegate(bool success, string error) {
                        this.BeginInvoke(new Action(delegate {
                            if (success)
                            {
                                _isInstalled = true;
                                _progressBar.Value = 100;
                                _lblStatus.Text = "WhatsLite has been successfully installed!";
                                _lblStatus.ForeColor = Color.FromArgb(18, 140, 126);
                                _lblHeaderTitle.Text = "Installation Complete!";
                                _lblHeaderSubtitle.Text = "WhatsLite is ready to use on your computer.";

                                _btnInstall.Text = "Finish";
                                _btnInstall.Enabled = true;
                                _btnCancel.Visible = false;
                            }
                            else
                            {
                                MessageBox.Show("Installation failed:\n" + error, "WhatsLite Setup Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                _btnInstall.Enabled = true;
                                _btnCancel.Enabled = true;
                                _txtDest.Enabled = true;
                                _btnBrowse.Enabled = true;
                            }
                        }));
                    }
                );
            });
        }
    }
}
