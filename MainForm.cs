using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace WhatsLite
{
    public class MainForm : Form
    {
        public static MainForm Instance { get; private set; }

        private const string AppTitle = "WhatsLite";
        private const string WhatsAppUrl = "https://web.whatsapp.com/";
        private const string RunRegistryKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string RunValueName = "WhatsLite";

        // Inter-process message for single-instance activation
        public static readonly uint WM_SHOW_WHATSLITE = RegisterWindowMessage("WhatsLite_ShowExistingInstance_Msg");

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern uint RegisterWindowMessage(string lpString);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool ShowWindowAsync(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        private static extern bool BringWindowToTop(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, IntPtr ProcessId);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("user32.dll")]
        private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

        private const int SW_RESTORE = 9;
        private const int SW_SHOW = 5;

        private WebView2 _webView;
        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _menuItemOpen;
        private ToolStripMenuItem _menuItemReload;
        private ToolStripMenuItem _menuItemStartWithWindows;
        private ToolStripMenuItem _menuItemLowMemory;
        private ToolStripMenuItem _menuItemQuit;

        private Icon _baseIcon;
        private Bitmap _baseBitmap;
        private Icon _currentTrayIcon;
        private WindowSettings _settings;
        private int _currentUnreadCount = 0;
        private bool _isQuitting = false;
        private bool _startHidden = false;
        private bool _hasShown = false;

        public MainForm(bool startHidden)
        {
            Instance = this;
            _startHidden = startHidden;
            _settings = WindowSettings.Load();

            InitializeComponent();
            ApplyWindowSettings();
        }

        private void InitializeComponent()
        {
            this.SuspendLayout();

            this.Text = AppTitle;
            this.StartPosition = FormStartPosition.Manual;
            this.MinimumSize = new Size(950, 600);
            this.AllowDrop = true;

            // Load crisp application icon
            string icoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "app.ico");
            if (File.Exists(icoPath))
            {
                try
                {
                    _baseIcon = new Icon(icoPath, 32, 32);
                    _baseBitmap = _baseIcon.ToBitmap();
                    this.Icon = _baseIcon;
                }
                catch {}
            }
            if (_baseIcon == null)
            {
                _baseIcon = SystemIcons.Application;
                _baseBitmap = _baseIcon.ToBitmap();
                this.Icon = _baseIcon;
            }

            // Setup System Tray Context Menu
            _trayMenu = new ContextMenuStrip();
            _trayMenu.RenderMode = ToolStripRenderMode.System;
            _trayMenu.ShowImageMargin = true;

            // Tray menu item with same icon as the taskbar/app icon
            _menuItemOpen = new ToolStripMenuItem("Open WhatsLite");
            _menuItemOpen.Image = new Bitmap(_baseBitmap, new Size(16, 16));
            _menuItemOpen.Font = new Font(_trayMenu.Font, FontStyle.Bold);
            _menuItemOpen.Click += delegate { ShowFromTray(); };

            _menuItemReload = new ToolStripMenuItem("Reload");
            _menuItemReload.Click += delegate {
                if (_webView != null && _webView.CoreWebView2 != null)
                {
                    _webView.CoreWebView2.Reload();
                }
            };

            _menuItemStartWithWindows = new ToolStripMenuItem("Start with Windows");
            _menuItemStartWithWindows.CheckOnClick = true;
            _menuItemStartWithWindows.Checked = IsStartWithWindowsEnabled();
            _menuItemStartWithWindows.Click += delegate {
                ToggleStartWithWindows(_menuItemStartWithWindows.Checked);
            };

            _menuItemLowMemory = new ToolStripMenuItem("Automatic Low-Memory Mode");
            _menuItemLowMemory.CheckOnClick = true;
            _menuItemLowMemory.Checked = _settings.LowMemoryMode;
            _menuItemLowMemory.Click += delegate {
                _settings.LowMemoryMode = _menuItemLowMemory.Checked;
                _settings.Save();
            };

            _menuItemQuit = new ToolStripMenuItem("Quit WhatsLite");
            _menuItemQuit.Click += delegate {
                _isQuitting = true;
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                this.Close();
                Application.Exit();
            };

            _trayMenu.Items.Add(_menuItemOpen);
            _trayMenu.Items.Add(_menuItemReload);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_menuItemStartWithWindows);
            _trayMenu.Items.Add(_menuItemLowMemory);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_menuItemQuit);

            // Setup System Tray Icon
            // By default, only visible when app is closed to tray via [X] or started with --tray
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = AppTitle;
            _notifyIcon.Icon = _baseIcon;
            _notifyIcon.ContextMenuStrip = _trayMenu;
            _notifyIcon.Visible = _startHidden; // Only show initially if started with --tray
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.DoubleClick += delegate { ShowFromTray(); };

            // Setup WebView2 Control
            _webView = new WebView2();
            _webView.Dock = DockStyle.Fill;
            this.Controls.Add(_webView);

            this.FormClosing += MainForm_FormClosing;
            this.Activated += MainForm_Activated;
            this.Resize += MainForm_Resize;
            this.LocationChanged += MainForm_LocationChanged;

            this.ResumeLayout(false);

            // Initialize WebView2 asynchronously
            InitializeWebViewAsync();
        }

        private async void InitializeWebViewAsync()
        {
            try
            {
                // Persistent User Data Folder
                string dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WhatsLite",
                    "UserData"
                );
                if (!Directory.Exists(dataFolder))
                {
                    Directory.CreateDirectory(dataFolder);
                }

                // WebView2 Performance and Tuning options
                string arguments = string.Join(" ", new string[] {
                    "--disable-features=Translate,OptimizationHints,MediaRouter,DialMediaRouteProvider,CalculateNativeWinOcclusion",
                    "--enable-features=MemorySaverMode,FluentOverlayScrollbars,ParallelDownloading,WebRtcHideLocalIpsWithMdns",
                    "--renderer-process-limit=3",
                    "--disable-background-networking=false",
                    "--enable-gpu-rasterization",
                    "--enable-zero-copy"
                });

                CoreWebView2EnvironmentOptions options = new CoreWebView2EnvironmentOptions();
                options.AdditionalBrowserArguments = arguments;

                CoreWebView2Environment env = await CoreWebView2Environment.CreateAsync(null, dataFolder, options);
                await _webView.EnsureCoreWebView2Async(env);

                ConfigureWebViewSettings();
                _webView.CoreWebView2.Navigate(WhatsAppUrl);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Failed to initialize WhatsLite WebView2 engine:\n" + ex.Message,
                    "WhatsLite Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }

        private void ConfigureWebViewSettings()
        {
            CoreWebView2 wv = _webView.CoreWebView2;
            if (wv == null) return;

            // Modern Chrome User-Agent on Windows 10/11
            wv.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0";
            wv.Settings.AreDevToolsEnabled = false;
            wv.Settings.AreDefaultContextMenusEnabled = true;
            wv.Settings.IsStatusBarEnabled = false;
            wv.Settings.AreBrowserAcceleratorKeysEnabled = true;

            // Permissions handling: auto-grant microphone, camera, notification, clipboard to WhatsApp
            wv.PermissionRequested += WebView_PermissionRequested;

            // Clean context menu
            wv.ContextMenuRequested += WebView_ContextMenuRequested;

            // External links in default browser
            wv.NewWindowRequested += WebView_NewWindowRequested;
            wv.NavigationStarting += WebView_NavigationStarting;

            // Unread count and title badge handling
            wv.DocumentTitleChanged += WebView_DocumentTitleChanged;

            // Web message handling for notification clicks
            wv.WebMessageReceived += WebView_WebMessageReceived;

            // Injected script to hook notifications for foreground activation on click
            string initScript = @"
                (function() {
                    if (window.Notification) {
                        var OrigNotif = window.Notification;
                        window.Notification = function(title, options) {
                            var notif = new OrigNotif(title, options);
                            notif.addEventListener('click', function() {
                                window.focus();
                                try {
                                    if (window.chrome && window.chrome.webview) {
                                        window.chrome.webview.postMessage('notification_click');
                                    }
                                } catch (e) {}
                            });
                            return notif;
                        };
                        window.Notification.permission = OrigNotif.permission;
                        window.Notification.requestPermission = OrigNotif.requestPermission;
                    }
                })();
            ";
            wv.AddScriptToExecuteOnDocumentCreatedAsync(initScript);
        }

        private void WebView_PermissionRequested(object sender, CoreWebView2PermissionRequestedEventArgs e)
        {
            try
            {
                Uri uri = new Uri(e.Uri);
                bool isWhatsApp = uri.Host.EndsWith("whatsapp.com", StringComparison.OrdinalIgnoreCase) ||
                                  uri.Host.EndsWith("whatsapp.net", StringComparison.OrdinalIgnoreCase);

                if (isWhatsApp)
                {
                    if (e.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                        e.PermissionKind == CoreWebView2PermissionKind.Camera ||
                        e.PermissionKind == CoreWebView2PermissionKind.Notifications ||
                        e.PermissionKind == CoreWebView2PermissionKind.ClipboardRead ||
                        e.PermissionKind == CoreWebView2PermissionKind.FileReadWrite)
                    {
                        e.State = CoreWebView2PermissionState.Allow;
                        e.Handled = true;
                        return;
                    }
                }

                // Deny unapproved permissions or other hosts
                e.State = CoreWebView2PermissionState.Deny;
                e.Handled = true;
            }
            catch
            {
                e.State = CoreWebView2PermissionState.Deny;
                e.Handled = true;
            }
        }

        private void WebView_ContextMenuRequested(object sender, CoreWebView2ContextMenuRequestedEventArgs e)
        {
            // Clean right-click menu: remove browser-only entries, keep copy, paste, spell check
            IList<CoreWebView2ContextMenuItem> items = e.MenuItems;
            for (int i = items.Count - 1; i >= 0; i--)
            {
                CoreWebView2ContextMenuItem item = items[i];
                string name = (item.Name ?? "").ToLowerInvariant();
                string label = (item.Label ?? "").ToLowerInvariant();

                bool isBrowserEntry =
                    name.Contains("back") || label.Contains("back") ||
                    name.Contains("forward") || label.Contains("forward") ||
                    name.Contains("reload") || name.Contains("refresh") || label.Contains("reload") ||
                    name.Contains("saveas") || name.Contains("saveimage") || name.Contains("savelink") ||
                    name.Contains("print") || label.Contains("print") ||
                    name.Contains("share") || label.Contains("share") ||
                    name.Contains("openlink") || name.Contains("openimage") ||
                    name.Contains("inspect") || label.Contains("inspect") ||
                    name.Contains("websearch") || label.Contains("bing") || label.Contains("google") ||
                    name.Contains("viewsource");

                if (isBrowserEntry)
                {
                    items.RemoveAt(i);
                }
            }

            // Clean up consecutive or boundary separators
            for (int i = items.Count - 1; i >= 0; i--)
            {
                if (items[i].Kind == CoreWebView2ContextMenuItemKind.Separator)
                {
                    if (i == 0 || i == items.Count - 1 || items[i - 1].Kind == CoreWebView2ContextMenuItemKind.Separator)
                    {
                        items.RemoveAt(i);
                    }
                }
            }

            if (items.Count == 0)
            {
                e.Handled = true;
            }
        }

        private void WebView_NewWindowRequested(object sender, CoreWebView2NewWindowRequestedEventArgs e)
        {
            e.Handled = true;
            if (!string.IsNullOrEmpty(e.Uri))
            {
                OpenExternalLink(e.Uri);
            }
        }

        private void WebView_NavigationStarting(object sender, CoreWebView2NavigationStartingEventArgs e)
        {
            if (e.IsUserInitiated && !string.IsNullOrEmpty(e.Uri))
            {
                Uri uri;
                if (Uri.TryCreate(e.Uri, UriKind.Absolute, out uri))
                {
                    if (!uri.Host.EndsWith("whatsapp.com", StringComparison.OrdinalIgnoreCase) &&
                        !uri.Host.EndsWith("whatsapp.net", StringComparison.OrdinalIgnoreCase))
                    {
                        e.Cancel = true;
                        OpenExternalLink(e.Uri);
                    }
                }
            }
        }

        private void OpenExternalLink(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
            catch {}
        }

        private void WebView_DocumentTitleChanged(object sender, object e)
        {
            if (_webView == null || _webView.CoreWebView2 == null) return;
            string docTitle = _webView.CoreWebView2.DocumentTitle ?? "";
            UpdateUnreadCountFromTitle(docTitle);
        }

        private void UpdateUnreadCountFromTitle(string title)
        {
            int count = 0;
            Match m = Regex.Match(title, @"^\((\d+)\)");
            if (m.Success)
            {
                int.TryParse(m.Groups[1].Value, out count);
            }

            bool isNewMessage = (count > _currentUnreadCount);
            _currentUnreadCount = count;

            // Window title
            if (count > 0)
            {
                this.Text = string.Format("({0}) {1}", count, AppTitle);
                _notifyIcon.Text = string.Format("({0}) {1}", count, AppTitle);
            }
            else
            {
                this.Text = AppTitle;
                _notifyIcon.Text = AppTitle;
            }

            // System tray icon badge (matching taskbar badge)
            UpdateTrayIcon(count);

            // Number badge on taskbar
            TaskbarHelper.SetOverlayBadge(this.Handle, count);

            // Taskbar button flashes when new message arrives while window is in background
            if (isNewMessage)
            {
                bool isBackground = (!this.Visible || this.WindowState == FormWindowState.Minimized || GetForegroundWindow() != this.Handle);
                if (isBackground)
                {
                    TaskbarHelper.FlashWindow(this.Handle);
                }
            }
        }

        private void UpdateTrayIcon(int count)
        {
            try
            {
                if (count <= 0)
                {
                    _notifyIcon.Icon = _baseIcon;
                }
                else
                {
                    Icon newIcon = TrayBadgeHelper.CreateTrayIconWithBadge(_baseBitmap, _baseIcon, count);
                    if (newIcon != null)
                    {
                        _notifyIcon.Icon = newIcon;
                        if (_currentTrayIcon != null && _currentTrayIcon != _baseIcon)
                        {
                            _currentTrayIcon.Dispose();
                        }
                        _currentTrayIcon = newIcon;
                    }
                }
            }
            catch {}
        }

        private void WebView_WebMessageReceived(object sender, CoreWebView2WebMessageReceivedEventArgs e)
        {
            try
            {
                string msg = e.TryGetWebMessageAsString();
                if (msg != null && msg.Contains("notification_click"))
                {
                    ShowFromTray();
                }
            }
            catch {}
        }

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                ShowFromTray();
            }
        }

        public void ShowFromTray()
        {
            // When restored, hide the tray icon so it doesn't clutter the notification area
            _notifyIcon.Visible = false;

            if (!this.Visible)
            {
                this.Show();
            }

            if (this.WindowState == FormWindowState.Minimized)
            {
                this.WindowState = _settings.IsMaximized ? FormWindowState.Maximized : FormWindowState.Normal;
            }

            this.ShowInTaskbar = true;

            IntPtr hWnd = this.Handle;
            ShowWindowAsync(hWnd, SW_RESTORE);

            // Bulletproof foreground activation in Windows 10/11
            try
            {
                IntPtr fgWnd = GetForegroundWindow();
                uint fgThread = GetWindowThreadProcessId(fgWnd, IntPtr.Zero);
                uint curThread = GetCurrentThreadId();

                if (fgThread != curThread && fgThread != 0)
                {
                    AttachThreadInput(curThread, fgThread, true);
                    SetForegroundWindow(hWnd);
                    BringWindowToTop(hWnd);
                    AttachThreadInput(curThread, fgThread, false);
                }
                else
                {
                    SetForegroundWindow(hWnd);
                    BringWindowToTop(hWnd);
                }
            }
            catch
            {
                SetForegroundWindow(hWnd);
            }

            this.Activate();
            this.BringToFront();

            TaskbarHelper.StopFlashing(this.Handle);
        }

        public void HideToTray()
        {
            SaveCurrentBounds();
            this.Hide();
            this.ShowInTaskbar = false;

            // Only show the tray icon when closed to tray via [X] or started with --tray!
            _notifyIcon.Visible = true;

            // Trigger low-memory optimization while hidden in tray
            if (_settings.LowMemoryMode)
            {
                Task.Factory.StartNew(delegate {
                    System.Threading.Thread.Sleep(500);
                    MemoryOptimizer.TrimMemory();
                });
            }
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!_isQuitting && e.CloseReason == CloseReason.UserClosing)
            {
                // [X] button closes window to system tray.
                // Minimizing is normal (stays on taskbar).
                // App is completely closed only via tray menu (Quit WhatsLite).
                e.Cancel = true;
                HideToTray();
                return;
            }

            // Persist settings on actual exit
            Instance = null;
            SaveCurrentBounds();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
            }
        }

        private void MainForm_Activated(object sender, EventArgs e)
        {
            TaskbarHelper.StopFlashing(this.Handle);
        }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                _settings.IsMaximized = false;
                _settings.Width = this.Width;
                _settings.Height = this.Height;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                _settings.IsMaximized = true;
            }
        }

        private void MainForm_LocationChanged(object sender, EventArgs e)
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                _settings.X = this.Left;
                _settings.Y = this.Top;
            }
        }

        private void SaveCurrentBounds()
        {
            if (this.WindowState == FormWindowState.Normal)
            {
                _settings.X = this.Left;
                _settings.Y = this.Top;
                _settings.Width = this.Width;
                _settings.Height = this.Height;
                _settings.IsMaximized = false;
            }
            else if (this.WindowState == FormWindowState.Maximized)
            {
                _settings.X = this.RestoreBounds.Left;
                _settings.Y = this.RestoreBounds.Top;
                _settings.Width = this.RestoreBounds.Width;
                _settings.Height = this.RestoreBounds.Height;
                _settings.IsMaximized = true;
            }
            _settings.Save();
        }

        private void ApplyWindowSettings()
        {
            Rectangle savedRect = new Rectangle(_settings.X, _settings.Y, _settings.Width, _settings.Height);
            bool isVisibleOnAnyScreen = false;
            foreach (Screen s in Screen.AllScreens)
            {
                if (s.WorkingArea.IntersectsWith(savedRect))
                {
                    isVisibleOnAnyScreen = true;
                    break;
                }
            }

            // Ensure window width is at least 1280 to show full 3-column WhatsApp layout
            if (isVisibleOnAnyScreen && _settings.Width >= 1280)
            {
                this.Bounds = savedRect;
            }
            else
            {
                Rectangle wa = Screen.PrimaryScreen.WorkingArea;
                int targetW = Math.Min(1320, Math.Max(1000, (int)(wa.Width * 0.88)));
                int targetH = Math.Min(780, Math.Max(650, (int)(wa.Height * 0.94)));
                this.Size = new Size(targetW, targetH);
                this.Location = new Point(wa.X + (wa.Width - targetW) / 2, wa.Y + (wa.Height - targetH) / 2);
            }

            if (_settings.IsMaximized)
            {
                this.WindowState = FormWindowState.Maximized;
            }
        }

        protected override void SetVisibleCore(bool value)
        {
            if (_startHidden && !_hasShown)
            {
                value = false;
                if (!this.IsHandleCreated) CreateHandle();
            }
            base.SetVisibleCore(value);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _hasShown = true;
            if (_startHidden)
            {
                HideToTray();
                _startHidden = false;
            }
            else
            {
                // When app starts normally and is visible, keep tray icon hidden
                _notifyIcon.Visible = false;
            }
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_SHOW_WHATSLITE)
            {
                ShowFromTray();
                return;
            }
            base.WndProc(ref m);
        }

        private bool IsStartWithWindowsEnabled()
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, false))
                {
                    if (key != null)
                    {
                        object val = key.GetValue(RunValueName);
                        return val != null;
                    }
                }
            }
            catch {}
            return false;
        }

        private void ToggleStartWithWindows(bool enable)
        {
            try
            {
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunRegistryKey, true))
                {
                    if (key != null)
                    {
                        if (enable)
                        {
                            string exePath = Application.ExecutablePath;
                            key.SetValue(RunValueName, "\"" + exePath + "\" --tray");
                            _settings.StartWithWindows = true;
                        }
                        else
                        {
                            key.DeleteValue(RunValueName, false);
                            _settings.StartWithWindows = false;
                        }
                        _settings.Save();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not update startup registry key:\n" + ex.Message, "WhatsLite", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }
    }
}
