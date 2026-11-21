# WhatsLite — Lightweight Windows Desktop Client for WhatsApp Web

**WhatsLite** is a blazingly fast, native Windows desktop client for WhatsApp Web built using Microsoft's evergreen **WebView2** runtime. Instead of bundling a 200+ MB Chromium/Electron browser, WhatsLite leverages the WebView2 engine already built into Windows 10 and 11, resulting in a single executable with sub-second startup and minimal memory footprint.

---
WhatsLite v1.1 — Standalone Multi-Account Desktop Client
🚀 What's New in v1.1:
Clean Minimalist Headers:
Main title bar shows strictly WhatsLite (no clutter or long usernames).
In-app menu shows strictly Switch Account ▾ (account names appear cleanly inside the dropdown).
Smart Login & Account Naming:
Displays No Account Logged In on fresh installations or unauthenticated accounts.
Automatically detects when you finish scanning the QR code and prompts you with a clean popup to name the account (e.g. Personal, Work).
➕ Add Account... is hidden until at least one account is linked.
Delete Single Account:
Full support to delete any account (single or multi-account) via 🗑️ Delete Account. Deleting resets the session and clears all cookies to display a fresh QR code screen.
Active-Account-Only Notifications:
Desktop notifications, audio chimes, taskbar flashing, and unread badge overlays trigger strictly for the active account. Inactive background accounts remain muted.
Standalone Installer v1.1:
Single standalone setup file (WhatsLiteSetup.exe, ~389 KB) with automatic Desktop & Start Menu shortcut creation and Windows Add/Remove Programs integration.

---

## Window Lifecycle & System Tray Behavior

- **Normal Windows Minimize (`[-]`)**:
  - Minimizing the window behaves like any standard Windows desktop app: it minimizes directly to the Windows taskbar.
  - The system tray icon is **not** displayed when minimized to taskbar.
- **Close Button (`[X]`) Sends to System Tray**:
  - Clicking the close button (`[X]`) hides the window and taskbar button, sending WhatsLite to the system tray.
  - The **system tray icon appears**, maintaining your persistent background connection for incoming calls, messages, and notifications.
- **Restore with a Single Click**:
  - Left-clicking the tray icon immediately restores WhatsLite to your screen and hides the tray icon again.
  - Opening WhatsLite from **any shortcut or pinned taskbar icon** while in the tray instantly restores the window to the foreground.
- **Quit via the Tray Menu**:
  - To completely terminate and exit the application, right-click the system tray icon and select **Quit WhatsLite**.

---

## Key Features

- **QR Code Login with Persistent Session**:
  - Scan the QR code once; your session is stored securely in `%APPDATA%\WhatsLite\UserData`. You remain signed in across reboots and restarts.
- **Full Responsive Layout (1320 × 780)**:
  - Default window dimensions are optimized for WhatsApp Web's 3-column layout (Navigation Rail + Chat List + Active Conversation + Right-Side Contact/Chat Drawer), preventing panels and action buttons from being hidden.
- **Unread Badge & Background Flashing**:
  - **Taskbar Number Badge**: Native Windows `ITaskbarList3` overlay icon showing the exact unread message count (e.g., `1`, `5`, `99+`).
  - **Tray Icon Badge**: The system tray icon dynamically updates with a matching badge showing the unread count when closed to tray.
  - **Window Title Count**: Updates dynamically to `(3) WhatsLite` or `WhatsLite`.
  - **Taskbar Flashing (`FlashWindowEx`)**: Smoothly flashes the taskbar button when a new message arrives while the window is in the background.
- **Zero-Prompt Voice & Video Calls**:
  - Microphone, camera, notification, and clipboard permissions are automatically granted exclusively to `https://web.whatsapp.com` (all other origins are strictly blocked).
- **Notifications Out-of-the-Box**:
  - Windows Toast and web notifications work automatically without manual prompts.
  - Clicking any incoming notification immediately restores WhatsLite and brings it to the front.
- **Single Instance Enforcement**:
  - Launching WhatsLite again detects the running instance via a system mutex and brings the existing window to the front without spawning duplicate processes.
- **Start with Windows**:
  - Toggle "Start with Windows" directly from the tray context menu (`HKCU\Software\Microsoft\Windows\CurrentVersion\Run`). When launched at system boot, it starts silently minimized in the system tray (`--tray`).
- **Clean Right-Click Menu**:
  - Stripped of unwanted browser entries (*Back*, *Forward*, *Reload*, *Print*, *Save As*, *Inspect Element*, *Search with Bing*).
  - Preserves essential editor actions: *Copy*, *Cut*, *Paste*, *Paste and Match Style*, *Select All*, *Undo*, *Redo*, and inline *Spell Check suggestions*.
- **Drag & Drop**:
  - Drag files, pictures, videos, and documents directly from Windows Explorer into chat conversations.
- **External Links in Default Browser**:
  - Web links clicked inside WhatsApp chats automatically launch in your default Windows browser (Edge, Chrome, Firefox, Brave, etc.) rather than inside WhatsLite.
- **Automatic Low-Memory Mode**:
  - When hidden to the system tray, WhatsLite automatically trims physical working set memory (`EmptyWorkingSet` + `SetProcessWorkingSetSize`), dropping active RAM to ~4 MB while preserving the background WebSocket connection for real-time notifications.

---

## Building from Source

1. **Build Main Executable**: Run [build.bat](file:///C:/Users/Dasdi/.gemini/antigravity/scratch/WhatsLite/build.bat) to compile [WhatsLite.exe](file:///C:/Users/Dasdi/.gemini/antigravity/scratch/WhatsLite/WhatsLite.exe).
2. **Build Standalone Installer**: Run [build_installer.bat](file:///C:/Users/Dasdi/.gemini/antigravity/scratch/WhatsLite/build_installer.bat) to package and generate [WhatsLiteSetup.exe](file:///C:/Users/Dasdi/.gemini/antigravity/scratch/WhatsLite/WhatsLiteSetup.exe).
