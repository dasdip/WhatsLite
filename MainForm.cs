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

        // UI & Account Controls
        private MenuStrip _mainMenu;
        private ToolStripMenuItem _menuAccounts;
        private ToolStripMenuItem _headerMuteBtn;
        private Panel _webContainer;
        private Dictionary<string, WebView2> _accountWebViews;
        private AccountManager _accountManager;

        private NotifyIcon _notifyIcon;
        private ContextMenuStrip _trayMenu;
        private ToolStripMenuItem _trayMenuOpen;
        private ToolStripMenuItem _trayMenuAccounts;
        private ToolStripMenuItem _trayMenuReload;
        private ToolStripMenuItem _trayMenuMuteSound;
        private ToolStripMenuItem _trayMenuStartWithWindows;
        private ToolStripMenuItem _trayMenuLowMemory;
        private ToolStripMenuItem _trayMenuQuit;

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
            _accountManager = AccountManager.Load();
            _accountWebViews = new Dictionary<string, WebView2>();

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
            this.BackColor = Color.FromArgb(17, 27, 33); // WhatsApp Dark

            // Load application icon
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

            // 1. In-App Dark Menu Bar (Option 2)
            _mainMenu = new MenuStrip();
            _mainMenu.Renderer = new DarkMenuRenderer();
            _mainMenu.BackColor = Color.FromArgb(24, 34, 41);
            _mainMenu.ForeColor = Color.FromArgb(233, 237, 239);
            _mainMenu.Font = new Font("Segoe UI", 9.0f);
            _mainMenu.ShowItemToolTips = true;

            _menuAccounts = new ToolStripMenuItem("Switch Account");
            _menuAccounts.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            _mainMenu.Items.Add(_menuAccounts);

            // Right-aligned Mute/Unmute Header Button (left side of minimize button)
            _headerMuteBtn = new ToolStripMenuItem();
            _headerMuteBtn.Alignment = ToolStripItemAlignment.Right;
            _headerMuteBtn.Font = new Font("Segoe UI", 9.0f, FontStyle.Bold);
            _headerMuteBtn.Click += delegate {
                _settings.MuteNotificationSound = !_settings.MuteNotificationSound;
                _settings.Save();
                ApplyMuteNotificationSound();
                UpdateMuteButtonsUI();
            };
            _mainMenu.Items.Add(_headerMuteBtn);
            this.MainMenuStrip = _mainMenu;
            this.Controls.Add(_mainMenu);

            // 2. Container Panel for WebViews
            _webContainer = new Panel();
            _webContainer.Dock = DockStyle.Fill;
            this.Controls.Add(_webContainer);
            _webContainer.BringToFront();

            // 3. System Tray Menu
            _trayMenu = new ContextMenuStrip();
            _trayMenu.Renderer = new DarkMenuRenderer();
            _trayMenu.ShowImageMargin = true;

            _trayMenuOpen = new ToolStripMenuItem("Open WhatsLite");
            _trayMenuOpen.Image = new Bitmap(_baseBitmap, new Size(16, 16));
            _trayMenuOpen.Font = new Font(_trayMenu.Font, FontStyle.Bold);
            _trayMenuOpen.Click += delegate { ShowFromTray(); };

            _trayMenuAccounts = new ToolStripMenuItem("Switch Account");

            _trayMenuReload = new ToolStripMenuItem("Reload");
            _trayMenuReload.Click += delegate {
                WebView2 activeWv = GetActiveWebView();
                if (activeWv != null && activeWv.CoreWebView2 != null)
                {
                    activeWv.CoreWebView2.Reload();
                }
            };

            _trayMenuMuteSound = new ToolStripMenuItem("Mute Notification Sound");
            _trayMenuMuteSound.CheckOnClick = true;
            _trayMenuMuteSound.Checked = _settings.MuteNotificationSound;
            _trayMenuMuteSound.Click += delegate {
                _settings.MuteNotificationSound = _trayMenuMuteSound.Checked;
                _settings.Save();
                ApplyMuteNotificationSound();
                UpdateMuteButtonsUI();
            };

            _trayMenuStartWithWindows = new ToolStripMenuItem("Start with Windows");
            _trayMenuStartWithWindows.CheckOnClick = true;
            _trayMenuStartWithWindows.Checked = IsStartWithWindowsEnabled();
            _trayMenuStartWithWindows.Click += delegate {
                ToggleStartWithWindows(_trayMenuStartWithWindows.Checked);
            };

            _trayMenuLowMemory = new ToolStripMenuItem("Automatic Low-Memory Mode");
            _trayMenuLowMemory.CheckOnClick = true;
            _trayMenuLowMemory.Checked = _settings.LowMemoryMode;
            _trayMenuLowMemory.Click += delegate {
                _settings.LowMemoryMode = _trayMenuLowMemory.Checked;
                _settings.Save();
            };

            _trayMenuQuit = new ToolStripMenuItem("Quit WhatsLite");
            _trayMenuQuit.Click += delegate {
                _isQuitting = true;
                if (_notifyIcon != null)
                {
                    _notifyIcon.Visible = false;
                    _notifyIcon.Dispose();
                }
                this.Close();
                Application.Exit();
            };

            _trayMenu.Items.Add(_trayMenuOpen);
            _trayMenu.Items.Add(_trayMenuAccounts);
            _trayMenu.Items.Add(_trayMenuReload);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayMenuMuteSound);
            _trayMenu.Items.Add(_trayMenuStartWithWindows);
            _trayMenu.Items.Add(_trayMenuLowMemory);
            _trayMenu.Items.Add(new ToolStripSeparator());
            _trayMenu.Items.Add(_trayMenuQuit);

            // 4. System Tray Icon
            _notifyIcon = new NotifyIcon();
            _notifyIcon.Text = AppTitle;
            _notifyIcon.Icon = _baseIcon;
            _notifyIcon.ContextMenuStrip = _trayMenu;
            _notifyIcon.Visible = true; // Always visible in system tray until manually closed
            _notifyIcon.MouseClick += NotifyIcon_MouseClick;
            _notifyIcon.DoubleClick += delegate { ShowFromTray(); };

            // Form Events
            this.FormClosing += MainForm_FormClosing;
            this.Activated += MainForm_Activated;
            this.Resize += MainForm_Resize;
            this.LocationChanged += MainForm_LocationChanged;

            // Network change detection for instant offline/online switching
            System.Net.NetworkInformation.NetworkChange.NetworkAvailabilityChanged += delegate(object s, System.Net.NetworkInformation.NetworkAvailabilityEventArgs ne) {
                try
                {
                    this.BeginInvoke(new Action(delegate {
                        WebView2 activeWv = GetActiveWebView();
                        if (activeWv != null && activeWv.CoreWebView2 != null)
                        {
                            if (!ne.IsAvailable)
                            {
                                activeWv.CoreWebView2.NavigateToString(GetOfflineHtml());
                            }
                            else
                            {
                                activeWv.CoreWebView2.Navigate(WhatsAppUrl);
                            }
                        }
                    }));
                }
                catch {}
            };

            this.ResumeLayout(false);
            this.PerformLayout();

            // Apply theme as per Windows color mode (Light/Dark)
            ApplyThemeBasedOnWindowsSettings();

            // Populate Accounts menu and launch active account
            RefreshAccountsMenu();
            InitializeActiveAccount();

            // Synchronize mute/unmute buttons across header and tray menu
            UpdateMuteButtonsUI();
        }

        private void InitializeActiveAccount()
        {
            AccountInfo activeAcc = _accountManager.GetActiveAccount();
            if (activeAcc != null)
            {
                GetOrCreateWebView(activeAcc);
                UpdateWindowTitle();
            }
        }

        private WebView2 GetOrCreateWebView(AccountInfo acc)
        {
            if (_accountWebViews.ContainsKey(acc.Id))
            {
                return _accountWebViews[acc.Id];
            }

            WebView2 wv = new WebView2();
            bool isDark = ThemeHelper.IsWindowsDarkTheme();
            wv.DefaultBackgroundColor = isDark ? Color.FromArgb(17, 27, 33) : Color.FromArgb(240, 242, 245);
            wv.Dock = DockStyle.Fill;
            bool isActive = (acc.Id == _accountManager.ActiveAccountId);
            wv.Visible = isActive;
            _webContainer.Controls.Add(wv);
            if (isActive) wv.BringToFront();

            _accountWebViews[acc.Id] = wv;
            InitializeWebViewForAccount(acc, wv);
            return wv;
        }

        private async void InitializeWebViewForAccount(AccountInfo acc, WebView2 wv)
        {
            try
            {
                string dataFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "WhatsLite",
                    acc.Folder
                );
                if (!Directory.Exists(dataFolder)) Directory.CreateDirectory(dataFolder);

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
                await wv.EnsureCoreWebView2Async(env);

                ConfigureWebViewSettings(acc, wv);

                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    wv.CoreWebView2.NavigateToString(GetOfflineHtml());
                }
                else
                {
                    wv.CoreWebView2.Navigate(WhatsAppUrl);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Failed to load WhatsApp for " + acc.Name + ":\n" + ex.Message, "WhatsLite Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void ConfigureWebViewSettings(AccountInfo acc, WebView2 wv)
        {
            CoreWebView2 core = wv.CoreWebView2;
            if (core == null) return;

            bool isActive = (acc.Id == _accountManager.ActiveAccountId);

            core.Settings.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36 Edg/130.0.0.0";
            core.Settings.AreDevToolsEnabled = false;
            core.Settings.AreDefaultContextMenusEnabled = true;
            core.Settings.IsStatusBarEnabled = false;
            core.Settings.AreBrowserAcceleratorKeysEnabled = true;
            core.Settings.IsBuiltInErrorPageEnabled = false;

            // Mute inactive account immediately to suppress background sounds, or active account if muted by user
            core.IsMuted = !isActive || _settings.MuteNotificationSound;

            // Permissions handling
            core.PermissionRequested += delegate(object sender, CoreWebView2PermissionRequestedEventArgs e) {
                WebView_PermissionRequested(acc, e);
            };

            // Clean context menu
            core.ContextMenuRequested += WebView_ContextMenuRequested;

            // External links
            core.NewWindowRequested += WebView_NewWindowRequested;
            core.NavigationStarting += WebView_NavigationStarting;

            // Navigation error -> show custom offline page
            core.NavigationCompleted += delegate(object sender, CoreWebView2NavigationCompletedEventArgs e) {
                if (!e.IsSuccess && e.WebErrorStatus != CoreWebView2WebErrorStatus.OperationCanceled)
                {
                    core.NavigateToString(GetOfflineHtml());
                }
            };

            // Document title change (Unread counter)
            core.DocumentTitleChanged += delegate(object sender, object e) {
                if (acc.Id == _accountManager.ActiveAccountId)
                {
                    string docTitle = core.DocumentTitle ?? "";
                    UpdateUnreadCountFromTitle(docTitle);
                }
            };

            // Notification clicks & Auth status
            core.WebMessageReceived += delegate(object sender, CoreWebView2WebMessageReceivedEventArgs e) {
                try
                {
                    string raw = e.TryGetWebMessageAsString();
                    if (string.IsNullOrEmpty(raw)) return;

                    if (raw.Contains("notification_click"))
                    {
                        if (acc.Id == _accountManager.ActiveAccountId)
                        {
                            ShowFromTray();
                        }
                    }
                    else if (raw.Contains("reload_request"))
                    {
                        if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                        {
                            core.NavigateToString(GetOfflineHtml());
                        }
                        else
                        {
                            core.Navigate(WhatsAppUrl);
                        }
                    }
                    else if (raw.Contains("network_offline"))
                    {
                        core.NavigateToString(GetOfflineHtml());
                    }
                    else if (raw.Contains("auth_status"))
                    {
                        Match mStatus = Regex.Match(raw, "\"status\"\\s*:\\s*\"([^\"]+)\"");
                        if (mStatus.Success)
                        {
                            string status = mStatus.Groups[1].Value;
                            if (status == "logged_out")
                            {
                                if (!acc.IsLoggedIn || acc.Name.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase))
                                {
                                    acc.IsLoggedIn = false;
                                    acc.Name = "No Account Logged In";
                                    _accountManager.Save();
                                    if (acc.Id == _accountManager.ActiveAccountId)
                                    {
                                        RefreshAccountsMenu();
                                        UpdateWindowTitle();
                                    }
                                }
                            }
                            else if (status == "logged_in")
                            {
                                bool wasNotLoggedIn = (!acc.IsLoggedIn || acc.Name.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase));
                                acc.IsLoggedIn = true;

                                if (wasNotLoggedIn)
                                {
                                    this.BeginInvoke(new Action(delegate {
                                        PromptNameOnLogin(acc);
                                    }));
                                }
                                else
                                {
                                    _accountManager.Save();
                                    if (acc.Id == _accountManager.ActiveAccountId)
                                    {
                                        RefreshAccountsMenu();
                                        UpdateWindowTitle();
                                    }
                                }
                            }
                        }
                    }
                }
                catch {}
            };

            // Injected script: Suppress notifications for inactive accounts & handle notification clicks & detect login state
            string initScript = @"
                (function() {
                    window.__whatslite_is_active = " + (isActive ? "true" : "false") + @";
                    if (window.Notification) {
                        var OrigNotif = window.Notification;
                        window.Notification = function(title, options) {
                            if (window.__whatslite_is_active === false) {
                                return {
                                    addEventListener: function() {},
                                    removeEventListener: function() {},
                                    close: function() {}
                                };
                            }
                            var notif = new OrigNotif(title, options);
                            notif.addEventListener('click', function() {
                                window.focus();
                                try {
                                    if (window.chrome && window.chrome.webview) {
                                        window.chrome.webview.postMessage(JSON.stringify({ type: 'notification_click' }));
                                    }
                                } catch (e) {}
                            });
                            return notif;
                        };
                        window.Notification.permission = OrigNotif.permission;
                        window.Notification.requestPermission = OrigNotif.requestPermission;
                    }

                    // Login State Detector
                    var lastAuthStatus = 'unknown';
                    function checkAuth() {
                        try {
                            var hasQR = !!(document.querySelector('canvas') ||
                                           document.querySelector('div[data-ref]') ||
                                           document.querySelector('[data-testid=""qrcode""]') ||
                                           (document.body && document.body.innerText && document.body.innerText.indexOf('Scan to log in') !== -1));

                            var hasChats = !!(document.querySelector('#pane-side') ||
                                             document.querySelector('[data-testid=""chat-list""]') ||
                                             document.querySelector('[data-testid=""cell-frame-container""]') ||
                                             document.querySelector('div[role=""navigation""]'));

                            if (hasChats && !hasQR) {
                                if (lastAuthStatus !== 'logged_in') {
                                    lastAuthStatus = 'logged_in';
                                    if (window.chrome && window.chrome.webview) {
                                        window.chrome.webview.postMessage(JSON.stringify({ type: 'auth_status', status: 'logged_in' }));
                                    }
                                }
                            } else if (hasQR) {
                                if (lastAuthStatus !== 'logged_out') {
                                    lastAuthStatus = 'logged_out';
                                    if (window.chrome && window.chrome.webview) {
                                        window.chrome.webview.postMessage(JSON.stringify({ type: 'auth_status', status: 'logged_out' }));
                                    }
                                }
                            }
                        } catch(e) {}
                    }

                    setInterval(checkAuth, 1000);
                    setTimeout(checkAuth, 1500);
                    setTimeout(checkAuth, 3500);

                    // Detect when browser goes offline
                    window.addEventListener('offline', function() {
                        try {
                            if (window.chrome && window.chrome.webview) {
                                window.chrome.webview.postMessage(JSON.stringify({ type: 'network_offline' }));
                            }
                        } catch(e) {}
                    });
                })();
            ";
            core.AddScriptToExecuteOnDocumentCreatedAsync(initScript);
        }

        private void RefreshAccountsMenu()
        {
            _menuAccounts.DropDownItems.Clear();
            _trayMenuAccounts.DropDownItems.Clear();

            // Second Header: strictly "Switch Account" - no user name!
            _menuAccounts.Text = "Switch Account ▾";
            _trayMenuAccounts.Text = "Switch Account";

            // 1. List accounts for switching
            foreach (AccountInfo acc in _accountManager.Accounts)
            {
                AccountInfo target = acc;
                bool isActive = (target.Id == _accountManager.ActiveAccountId);

                // MenuStrip item
                ToolStripMenuItem item = new ToolStripMenuItem(target.Name);
                if (isActive)
                {
                    item.Checked = true;
                    item.Font = new Font(_mainMenu.Font, FontStyle.Bold);
                }
                item.Click += delegate { SwitchToAccount(target.Id); };
                _menuAccounts.DropDownItems.Add(item);

                // Tray Menu item
                ToolStripMenuItem trayItem = new ToolStripMenuItem(target.Name);
                if (isActive)
                {
                    trayItem.Checked = true;
                }
                trayItem.Click += delegate { SwitchToAccount(target.Id); ShowFromTray(); };
                _trayMenuAccounts.DropDownItems.Add(trayItem);
            }

            // Check if at least one account is logged in
            bool hasLoggedInAccount = false;
            foreach (AccountInfo a in _accountManager.Accounts)
            {
                if (a.IsLoggedIn && !a.Name.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase))
                {
                    hasLoggedInAccount = true;
                    break;
                }
            }

            _menuAccounts.DropDownItems.Add(new ToolStripSeparator());
            _trayMenuAccounts.DropDownItems.Add(new ToolStripSeparator());

            // 2. ➕ Add Account... (ONLY shown if at least one account is logged in!)
            if (hasLoggedInAccount)
            {
                ToolStripMenuItem itemAdd = new ToolStripMenuItem("➕  Add Account...");
                itemAdd.Click += delegate { PromptAddAccount(); };
                _menuAccounts.DropDownItems.Add(itemAdd);

                ToolStripMenuItem trayItemAdd = new ToolStripMenuItem("➕  Add Account...");
                trayItemAdd.Click += delegate { ShowFromTray(); PromptAddAccount(); };
                _trayMenuAccounts.DropDownItems.Add(trayItemAdd);
            }

            // 3. ✏️ Edit user name
            if (_accountManager.Accounts.Count == 1)
            {
                AccountInfo singleAcc = _accountManager.Accounts[0];
                ToolStripMenuItem itemEdit = new ToolStripMenuItem(string.Format("✏️  Edit User Name ('{0}')...", singleAcc.Name));
                itemEdit.Click += delegate { PromptRenameSpecificAccount(singleAcc); };
                _menuAccounts.DropDownItems.Add(itemEdit);

                ToolStripMenuItem trayItemEdit = new ToolStripMenuItem(string.Format("✏️  Edit User Name ('{0}')...", singleAcc.Name));
                trayItemEdit.Click += delegate { ShowFromTray(); PromptRenameSpecificAccount(singleAcc); };
                _trayMenuAccounts.DropDownItems.Add(trayItemEdit);
            }
            else
            {
                ToolStripMenuItem itemEditGroup = new ToolStripMenuItem("✏️  Edit User Name");
                ToolStripMenuItem trayItemEditGroup = new ToolStripMenuItem("✏️  Edit User Name");

                foreach (AccountInfo acc in _accountManager.Accounts)
                {
                    AccountInfo targetAcc = acc;
                    bool isAct = (targetAcc.Id == _accountManager.ActiveAccountId);
                    string label = string.Format("✏️  Edit '{0}'{1}...", targetAcc.Name, isAct ? " (Active)" : "");

                    ToolStripMenuItem subEdit = new ToolStripMenuItem(label);
                    subEdit.Click += delegate { PromptRenameSpecificAccount(targetAcc); };
                    itemEditGroup.DropDownItems.Add(subEdit);

                    ToolStripMenuItem traySubEdit = new ToolStripMenuItem(label);
                    traySubEdit.Click += delegate { ShowFromTray(); PromptRenameSpecificAccount(targetAcc); };
                    trayItemEditGroup.DropDownItems.Add(traySubEdit);
                }
                _menuAccounts.DropDownItems.Add(itemEditGroup);
                _trayMenuAccounts.DropDownItems.Add(trayItemEditGroup);
            }

            // 4. 🗑️ Delete Account (for single account as well as multiple accounts)
            if (_accountManager.Accounts.Count == 1)
            {
                AccountInfo singleAcc = _accountManager.Accounts[0];
                ToolStripMenuItem itemDelete = new ToolStripMenuItem(string.Format("🗑️  Delete Account ('{0}')...", singleAcc.Name));
                itemDelete.Click += delegate { PromptDeleteSpecificAccount(singleAcc); };
                _menuAccounts.DropDownItems.Add(itemDelete);

                ToolStripMenuItem trayItemDelete = new ToolStripMenuItem(string.Format("🗑️  Delete Account ('{0}')...", singleAcc.Name));
                trayItemDelete.Click += delegate { ShowFromTray(); PromptDeleteSpecificAccount(singleAcc); };
                _trayMenuAccounts.DropDownItems.Add(trayItemDelete);
            }
            else
            {
                ToolStripMenuItem itemDeleteGroup = new ToolStripMenuItem("🗑️  Delete Account");
                ToolStripMenuItem trayItemDeleteGroup = new ToolStripMenuItem("🗑️  Delete Account");

                foreach (AccountInfo acc in _accountManager.Accounts)
                {
                    AccountInfo targetAcc = acc;
                    bool isAct = (targetAcc.Id == _accountManager.ActiveAccountId);
                    string label = string.Format("🗑️  Delete '{0}'{1}...", targetAcc.Name, isAct ? " (Active)" : "");

                    ToolStripMenuItem subDelete = new ToolStripMenuItem(label);
                    subDelete.Click += delegate { PromptDeleteSpecificAccount(targetAcc); };
                    itemDeleteGroup.DropDownItems.Add(subDelete);

                    ToolStripMenuItem traySubDelete = new ToolStripMenuItem(label);
                    traySubDelete.Click += delegate { ShowFromTray(); PromptDeleteSpecificAccount(targetAcc); };
                    trayItemDeleteGroup.DropDownItems.Add(traySubDelete);
                }
                _menuAccounts.DropDownItems.Add(itemDeleteGroup);
                _trayMenuAccounts.DropDownItems.Add(trayItemDeleteGroup);
            }

            // 5. ℹ️ About App section (positioned below all user names and Delete Account button)
            _menuAccounts.DropDownItems.Add(new ToolStripSeparator());
            _trayMenuAccounts.DropDownItems.Add(new ToolStripSeparator());

            ToolStripMenuItem itemAbout = new ToolStripMenuItem("ℹ️   About WhatsLite...");
            itemAbout.Click += delegate { ShowAboutDialog(); };
            _menuAccounts.DropDownItems.Add(itemAbout);

            ToolStripMenuItem trayItemAbout = new ToolStripMenuItem("ℹ️   About WhatsLite...");
            trayItemAbout.Click += delegate { ShowFromTray(); ShowAboutDialog(); };
            _trayMenuAccounts.DropDownItems.Add(trayItemAbout);
        }

        private void ShowAboutDialog()
        {
            using (AboutForm dlg = new AboutForm(_baseBitmap))
            {
                dlg.ShowDialog(this);
            }
        }

        private string GetOfflineHtml()
        {
            return @"<!DOCTYPE html>
<html>
<head>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"" />
    <title>Not Connected to the Internet</title>
    <style>
        * { box-sizing: border-box; margin: 0; padding: 0; user-select: none; }
        body {
            background-color: #111b21;
            background-image: radial-gradient(circle at 50% 35%, #182a32 0%, #111b21 72%);
            color: #e9edef;
            font-family: 'Segoe UI', -apple-system, BlinkMacSystemFont, Roboto, Helvetica, Arial, sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            margin: 0;
            overflow: hidden;
        }
        .container {
            text-align: center;
            padding: 40px 30px;
            max-width: 520px;
            animation: fadeIn 0.4s ease-out;
        }
        @keyframes fadeIn {
            from { opacity: 0; transform: translateY(10px); }
            to { opacity: 1; transform: translateY(0); }
        }
        .icon-wrapper {
            position: relative;
            width: 100px;
            height: 100px;
            margin: 0 auto 28px;
            display: flex;
            align-items: center;
            justify-content: center;
            background: rgba(32, 44, 51, 0.85);
            border: 2px solid #2a3942;
            border-radius: 50%;
            box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4), 0 0 20px rgba(0, 168, 132, 0.15);
        }
        .icon-wifi {
            width: 52px;
            height: 52px;
            fill: #00a884;
        }
        .icon-badge {
            position: absolute;
            bottom: -2px;
            right: -2px;
            width: 32px;
            height: 32px;
            background: #ea4335;
            border: 3px solid #111b21;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            color: #fff;
            font-size: 16px;
            font-weight: bold;
        }
        h1 {
            font-size: 23px;
            font-weight: 600;
            color: #e9edef;
            margin-bottom: 12px;
            letter-spacing: -0.2px;
        }
        p {
            font-size: 15px;
            color: #8696a0;
            line-height: 1.5;
            margin-bottom: 30px;
        }
        .btn-reload {
            display: inline-flex;
            align-items: center;
            justify-content: center;
            gap: 10px;
            background-color: #00a884;
            color: #ffffff;
            font-size: 15px;
            font-weight: 600;
            font-family: inherit;
            padding: 12px 32px;
            border: none;
            border-radius: 24px;
            cursor: pointer;
            outline: none;
            transition: all 0.2s ease;
            box-shadow: 0 4px 14px rgba(0, 168, 132, 0.35);
        }
        .btn-reload:hover {
            background-color: #02906f;
            transform: translateY(-1px);
            box-shadow: 0 6px 18px rgba(0, 168, 132, 0.45);
        }
        .btn-reload:active {
            background-color: #02785d;
            transform: translateY(1px);
            box-shadow: 0 2px 8px rgba(0, 168, 132, 0.3);
        }
        .btn-reload svg {
            width: 18px;
            height: 18px;
            fill: currentColor;
            transition: transform 0.4s ease;
        }
        .btn-reload:hover svg {
            transform: rotate(180deg);
        }
        .footer-note {
            margin-top: 24px;
            font-size: 12px;
            color: #667781;
        }
    </style>
</head>
<body>
    <div class=""container"">
        <div class=""icon-wrapper"">
            <svg class=""icon-wifi"" viewBox=""0 0 24 24"">
                <path d=""M12 4C7.31 4 3.07 5.9 0 8.98L12 21 24 8.98A16.88 16.88 0 0 0 12 4z"" opacity=""0.25""/>
                <path d=""M12 10c-3.03 0-5.78 1.23-7.76 3.22L12 21l7.76-7.78C17.78 11.23 15.03 10 12 10z"" opacity=""0.5""/>
                <circle cx=""12"" cy=""18"" r=""2""/>
            </svg>
            <div class=""icon-badge"">!</div>
        </div>
        <h1>Not Connected to the Internet</h1>
        <p>Click Reload when you Have Active Internet</p>
        <button id=""btnReload"" class=""btn-reload"" onclick=""doReload()"">
            <svg viewBox=""0 0 24 24"">
                <path d=""M17.65 6.35C16.2 4.9 14.21 4 12 4c-4.42 0-7.99 3.58-7.99 8s3.57 8 7.99 8c3.73 0 6.84-2.55 7.73-6h-2.08c-.82 2.33-3.04 4-5.65 4-3.31 0-6-2.69-6-6s2.69-6 6-6c1.66 0 3.14.69 4.22 1.78L13 11h7V4l-2.35 2.35z""/>
            </svg>
            <span>Reload</span>
        </button>
        <div class=""footer-note"">WhatsLite Desktop &bull; Automatically checks connection</div>
    </div>
    <script>
        function doReload() {
            var btn = document.getElementById('btnReload');
            if (btn) {
                btn.innerHTML = 'Reconnecting...';
                btn.style.opacity = '0.7';
                btn.disabled = true;
            }
            try {
                if (window.chrome && window.chrome.webview) {
                    window.chrome.webview.postMessage(JSON.stringify({ type: 'reload_request' }));
                    return;
                }
            } catch(e) {}
            window.location.href = 'https://web.whatsapp.com';
        }

        window.addEventListener('online', function() {
            doReload();
        });
    </script>
</body>
</html>";
        }

        public void SwitchToAccount(string accountId)
        {
            if (accountId == _accountManager.ActiveAccountId && _accountWebViews.ContainsKey(accountId))
            {
                return;
            }

            // 1. Deactivate current account: Mute sound, disable notifications
            AccountInfo oldAcc = _accountManager.GetActiveAccount();
            if (oldAcc != null && oldAcc.Id != accountId && _accountWebViews.ContainsKey(oldAcc.Id))
            {
                WebView2 oldWv = _accountWebViews[oldAcc.Id];
                oldWv.Visible = false;
                if (oldWv.CoreWebView2 != null)
                {
                    oldWv.CoreWebView2.IsMuted = true;
                    oldWv.CoreWebView2.ExecuteScriptAsync("window.__whatslite_is_active = false;");
                }
            }

            // 2. Set new active account
            _accountManager.ActiveAccountId = accountId;
            _accountManager.Save();

            // 3. Activate new account: Unmute, enable notifications, show single window
            AccountInfo newAcc = _accountManager.GetActiveAccount();
            if (newAcc != null)
            {
                WebView2 newWv = GetOrCreateWebView(newAcc);
                newWv.Visible = true;
                newWv.BringToFront();

                if (newWv.CoreWebView2 != null)
                {
                    newWv.CoreWebView2.IsMuted = _settings.MuteNotificationSound;
                    newWv.CoreWebView2.ExecuteScriptAsync("window.__whatslite_is_active = true;");
                    UpdateUnreadCountFromTitle(newWv.CoreWebView2.DocumentTitle ?? "");
                }
            }

            RefreshAccountsMenu();
            UpdateWindowTitle();
        }

        private void PromptAddAccount()
        {
            // If an unlogged account already exists, switch to it instead of making duplicates
            foreach (AccountInfo a in _accountManager.Accounts)
            {
                if (!a.IsLoggedIn || a.Name.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase))
                {
                    SwitchToAccount(a.Id);
                    return;
                }
            }

            AccountInfo newAcc = _accountManager.AddAccount();
            SwitchToAccount(newAcc.Id);
        }

        private void PromptNameOnLogin(AccountInfo acc)
        {
            if (acc == null) return;

            ShowFromTray();
            this.Activate();

            using (AccountNameDialog dlg = new AccountNameDialog(
                "WhatsApp Logged In",
                "WhatsApp linked successfully!\nPlease enter a name for this account:",
                ""))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string chosen = dlg.AccountName.Trim();
                    if (!string.IsNullOrEmpty(chosen))
                    {
                        acc.Name = chosen;
                    }
                    else
                    {
                        acc.Name = "Account " + (_accountManager.Accounts.IndexOf(acc) + 1);
                    }
                }
                else
                {
                    if (acc.Name.Equals("No Account Logged In", StringComparison.OrdinalIgnoreCase))
                    {
                        acc.Name = "Account " + (_accountManager.Accounts.IndexOf(acc) + 1);
                    }
                }

                acc.IsLoggedIn = true;
                _accountManager.Save();
                RefreshAccountsMenu();
                UpdateWindowTitle();
            }
        }

        private void PromptRenameSpecificAccount(AccountInfo targetAcc)
        {
            if (targetAcc == null) return;

            using (AccountNameDialog dlg = new AccountNameDialog(
                "Edit User Name",
                string.Format("Enter new user name for '{0}':", targetAcc.Name),
                targetAcc.Name))
            {
                if (dlg.ShowDialog(this) == DialogResult.OK)
                {
                    string newName = dlg.AccountName;
                    if (!string.IsNullOrEmpty(newName))
                    {
                        _accountManager.RenameAccount(targetAcc.Id, newName);
                        RefreshAccountsMenu();
                        UpdateWindowTitle();
                    }
                }
            }
        }

        private async void PromptDeleteSpecificAccount(AccountInfo targetAcc)
        {
            if (targetAcc == null) return;

            DialogResult dr = MessageBox.Show(
                string.Format("Are you sure you want to delete account '{0}'?\n\nThis will log you out and completely clear its session data on this PC.", targetAcc.Name),
                "Delete Account",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning
            );

            if (dr == DialogResult.Yes)
            {
                string removeId = targetAcc.Id;
                bool wasActive = (removeId == _accountManager.ActiveAccountId);

                if (_accountManager.Accounts.Count > 1)
                {
                    if (_accountWebViews.ContainsKey(removeId))
                    {
                        WebView2 wv = _accountWebViews[removeId];
                        _accountWebViews.Remove(removeId);
                        _webContainer.Controls.Remove(wv);
                        wv.Dispose();
                    }

                    _accountManager.RemoveAccount(removeId);

                    if (wasActive)
                    {
                        AccountInfo nextAcc = _accountManager.GetActiveAccount();
                        if (nextAcc != null)
                        {
                            SwitchToAccount(nextAcc.Id);
                        }
                    }
                    else
                    {
                        RefreshAccountsMenu();
                        UpdateWindowTitle();
                    }
                }
                else
                {
                    // Single remaining account: clear browsing data and reset to fresh logged-out state
                    _accountManager.ResetAccountToLoggedOut(removeId);

                    if (_accountWebViews.ContainsKey(removeId))
                    {
                        WebView2 wv = _accountWebViews[removeId];
                        if (wv.CoreWebView2 != null)
                        {
                            try
                            {
                                await wv.CoreWebView2.Profile.ClearBrowsingDataAsync();
                            }
                            catch {}

                            if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                            {
                                wv.CoreWebView2.NavigateToString(GetOfflineHtml());
                            }
                            else
                            {
                                wv.CoreWebView2.Navigate(WhatsAppUrl);
                            }
                        }
                    }

                    RefreshAccountsMenu();
                    UpdateWindowTitle();
                }
            }
        }

        private WebView2 GetActiveWebView()
        {
            AccountInfo acc = _accountManager.GetActiveAccount();
            if (acc != null && _accountWebViews.ContainsKey(acc.Id))
            {
                return _accountWebViews[acc.Id];
            }
            return null;
        }

        private void WebView_PermissionRequested(AccountInfo acc, CoreWebView2PermissionRequestedEventArgs e)
        {
            try
            {
                Uri uri = new Uri(e.Uri);
                bool isWhatsApp = uri.Host.EndsWith("whatsapp.com", StringComparison.OrdinalIgnoreCase) ||
                                  uri.Host.EndsWith("whatsapp.net", StringComparison.OrdinalIgnoreCase);

                if (isWhatsApp)
                {
                    // NOTIFICATION RULE: Only active account gets notification permission granted!
                    if (e.PermissionKind == CoreWebView2PermissionKind.Notifications)
                    {
                        if (acc.Id == _accountManager.ActiveAccountId)
                        {
                            e.State = CoreWebView2PermissionState.Allow;
                            e.Handled = true;
                            return;
                        }
                        else
                        {
                            e.State = CoreWebView2PermissionState.Deny;
                            e.Handled = true;
                            return;
                        }
                    }

                    if (e.PermissionKind == CoreWebView2PermissionKind.Microphone ||
                        e.PermissionKind == CoreWebView2PermissionKind.Camera ||
                        e.PermissionKind == CoreWebView2PermissionKind.ClipboardRead ||
                        e.PermissionKind == CoreWebView2PermissionKind.FileReadWrite)
                    {
                        e.State = CoreWebView2PermissionState.Allow;
                        e.Handled = true;
                        return;
                    }
                }

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
            if (!string.IsNullOrEmpty(e.Uri) && (e.Uri.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || e.Uri.StartsWith("https://", StringComparison.OrdinalIgnoreCase)))
            {
                if (!System.Net.NetworkInformation.NetworkInterface.GetIsNetworkAvailable())
                {
                    e.Cancel = true;
                    CoreWebView2 core = sender as CoreWebView2;
                    if (core != null)
                    {
                        core.NavigateToString(GetOfflineHtml());
                    }
                    return;
                }
            }

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

            UpdateWindowTitle();

            // System tray icon badge
            UpdateTrayIcon(count);

            // Number badge on taskbar
            TaskbarHelper.SetOverlayBadge(this.Handle, count);

            // Taskbar button flashes when new message arrives on active account while in background
            if (isNewMessage)
            {
                bool isBackground = (!this.Visible || this.WindowState == FormWindowState.Minimized || GetForegroundWindow() != this.Handle);
                if (isBackground)
                {
                    TaskbarHelper.FlashWindow(this.Handle);
                }
            }
        }

        private void UpdateWindowTitle()
        {
            if (_currentUnreadCount > 0)
            {
                this.Text = string.Format("({0}) {1}", _currentUnreadCount, AppTitle);
                _notifyIcon.Text = string.Format("({0}) {1}", _currentUnreadCount, AppTitle);
            }
            else
            {
                this.Text = AppTitle;
                _notifyIcon.Text = AppTitle;
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

        private void NotifyIcon_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                if (!this.Visible || this.WindowState == FormWindowState.Minimized || GetForegroundWindow() != this.Handle)
                {
                    ShowFromTray();
                }
                else
                {
                    HideToTray();
                }
            }
        }

        public void ShowFromTray()
        {
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
            _notifyIcon.Visible = true;

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
                e.Cancel = true;
                HideToTray();
                return;
            }

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
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            ApplyThemeBasedOnWindowsSettings();
        }

        protected override void WndProc(ref Message m)
        {
            const int WM_SETTINGCHANGE = 0x001A;
            const int WM_THEMECHANGED = 0x031A;
            if (m.Msg == WM_SETTINGCHANGE || m.Msg == WM_THEMECHANGED)
            {
                ApplyThemeBasedOnWindowsSettings();
            }

            if (m.Msg == WM_SHOW_WHATSLITE)
            {
                ShowFromTray();
                return;
            }
            base.WndProc(ref m);
        }

        private void ApplyThemeBasedOnWindowsSettings()
        {
            try
            {
                bool isDark = ThemeHelper.IsWindowsDarkTheme();

                // 1. Native Windows Title Bar / Header (Dark when Windows is Dark, White/Light when Light)
                ThemeHelper.ApplyTitleBarTheme(this.Handle, isDark);

                // 2. In-App Menu Bar Header
                if (_mainMenu != null)
                {
                    if (isDark)
                    {
                        _mainMenu.Renderer = new DarkMenuRenderer();
                        _mainMenu.BackColor = Color.FromArgb(24, 34, 41);
                        _mainMenu.ForeColor = Color.FromArgb(233, 237, 239);
                    }
                    else
                    {
                        _mainMenu.Renderer = new LightMenuRenderer();
                        _mainMenu.BackColor = Color.FromArgb(240, 242, 245);
                        _mainMenu.ForeColor = Color.FromArgb(17, 27, 33);
                    }
                    _mainMenu.Invalidate();
                }

                // 3. System Tray Menu
                if (_trayMenu != null)
                {
                    _trayMenu.Renderer = isDark ? (ToolStripRenderer)new DarkMenuRenderer() : new LightMenuRenderer();
                    _trayMenu.Invalidate();
                }

                // 4. Container & WebView default background
                Color webBg = isDark ? Color.FromArgb(17, 27, 33) : Color.FromArgb(240, 242, 245);
                if (_webContainer != null)
                {
                    _webContainer.BackColor = webBg;
                }
                if (_accountWebViews != null)
                {
                    foreach (var kvp in _accountWebViews)
                    {
                        if (kvp.Value != null)
                        {
                            kvp.Value.DefaultBackgroundColor = webBg;
                        }
                    }
                }
            }
            catch {}
        }

        private void ApplyMuteNotificationSound()
        {
            try
            {
                AccountInfo activeAcc = _accountManager.GetActiveAccount();
                if (_accountWebViews != null)
                {
                    foreach (var kvp in _accountWebViews)
                    {
                        if (kvp.Value != null && kvp.Value.CoreWebView2 != null)
                        {
                            bool isAct = (activeAcc != null && kvp.Key == activeAcc.Id);
                            kvp.Value.CoreWebView2.IsMuted = !isAct || _settings.MuteNotificationSound;
                        }
                    }
                }
            }
            catch {}
        }

        private void UpdateMuteButtonsUI()
        {
            try
            {
                bool isMuted = _settings.MuteNotificationSound;

                if (_trayMenuMuteSound != null)
                {
                    _trayMenuMuteSound.Checked = isMuted;
                }

                if (_headerMuteBtn != null)
                {
                    if (isMuted)
                    {
                        _headerMuteBtn.Text = "🔇  Muted";
                        _headerMuteBtn.ToolTipText = "Notification sounds are muted. Click to unmute.";
                        _headerMuteBtn.Tag = Color.FromArgb(241, 92, 109);
                    }
                    else
                    {
                        _headerMuteBtn.Text = "🔊  Sound";
                        _headerMuteBtn.ToolTipText = "Notification sounds are active. Click to mute.";
                        _headerMuteBtn.Tag = Color.FromArgb(0, 168, 132);
                    }
                }

                if (_mainMenu != null) _mainMenu.Invalidate();
                if (_trayMenu != null) _trayMenu.Invalidate();
            }
            catch {}
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
