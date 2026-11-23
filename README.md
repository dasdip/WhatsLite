# WhatsLite v1.3 — Lightweight Native Desktop Client for WhatsApp Web
> **Created & Developed by [Mr. Dipendu Das](https://github.com/)**

[![Release](https://img.shields.io/badge/Release-v1.3-brightgreen.svg)](https://github.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%2F%2011-blue.svg)](https://microsoft.com)
[![Engine](https://img.shields.io/badge/Engine-Microsoft%20WebView2-teal.svg)](https://developer.microsoft.com/microsoft-edge/webview2/)
[![RAM Usage](https://img.shields.io/badge/RAM%20Usage-~50%20MB-success.svg)](https://github.com/)
[![Installer Size](https://img.shields.io/badge/Installer%20Size-~385%20KB-orange.svg)](https://github.com/)
[![License](https://img.shields.io/badge/License-MIT-lightgrey.svg)](LICENSE)

**WhatsLite** is an ultra-fast, lightweight native Windows desktop client for WhatsApp Web built using Microsoft's evergreen **WebView2** runtime. 

Unlike traditional Electron-based wrappers that bundle a full 200+ MB Chromium browser and consume 400+ MB of RAM, WhatsLite taps directly into the high-performance WebView2 runtime already built into Windows 10 and 11. It delivers sub-second instant startup, near-zero idle memory consumption (~50 MB), true multi-account isolation, and seamless native Windows integration.

---

## ⚡ Electron vs. WhatsLite Comparison

| Feature | Standard Electron Apps | WhatsLite v1.3 |
|---|:---:|:---:|
| **Installer Size** | 150 MB – 250 MB | **~385 KB** |
| **Installed Disk Footprint** | 350 MB – 500 MB | **< 2 MB** |
| **RAM Usage (Idle)** | 400 MB – 650 MB | **~15 MB** |
| **Startup Time** | 3 – 6 seconds | **< 1 second** |
| **Multi-Account Support** | ❌ None (or clumsy tabs) | **✔️ True isolated accounts** |
| **Active-Only Notifications** | ❌ Background noise from all | **✔️ Mutes background accounts** |
| **Windows Theme Sync** | ❌ Inconsistent / manual | **✔️ Real-time Light/Dark sync** |
| **Offline Handling** | ❌ Ugly browser dinosaur | **✔️ Custom WhatsApp-themed page** |

---

## ✨ Complete Feature Breakdown

### 👥 1. True Multi-Account Management
* **100% Session Isolation**: Run multiple independent WhatsApp accounts (e.g. *Personal*, *Work*, *Client*) simultaneously. Each account has its own isolated cookie jar, local storage, and cache directory.
* **Smart Login Detection**:
  * Automatically detects unlinked accounts (`No Account Logged In`).
  * Prompt dialog pops up the moment a QR code is scanned, letting you name the account immediately.
  * The `➕ Add Account...` option only appears once your first account is linked.
* **Account Controls**:
  * `✏️ Edit User Name`: Easily rename accounts at any time.
  * `🗑️ Delete Account`: Completely clears stored login sessions and cookies with one click.
* **Minimalist Interface**:
  * Main window title displays strictly `WhatsLite` (or `(N) WhatsLite` when unread messages arrive).
  * In-app menu bar displays strictly `Switch Account ▾`. Account names only appear inside dropdown menus.

### 🌓 2. Dynamic Windows Light / Dark Theme Synchronization
* **Adaptive Title Bar & Menu Header**:
  * Automatically queries Windows color mode (`AppsUseLightTheme`).
  * **Dark Mode**: Activates native Windows immersive dark title bar (`DWMWA_USE_IMMERSIVE_DARK_MODE`) with dark `#182229` in-app header and dark dropdowns.
  * **Light Mode**: Automatically flips to clean white title bar with matching `#f0f2f5` light header and light dropdowns.
* **Live Real-Time Switching**: Listens to Windows `WM_SETTINGCHANGE` and `WM_THEMECHANGED` messages to switch themes in real-time when you toggle Dark/Light mode in Windows Settings — no restart required.
* **Unified Dialogs**: Modal windows (**About WhatsLite**, **Edit User Name**) automatically match the active Windows theme.

### 🌐 3. Dedicated Zero-Latency Offline Experience
* **Custom No-Internet Screen**:
  * When disconnected, WhatsLite completely suppresses Chromium's default dinosaur/error page (`IsBuiltInErrorPageEnabled = false`).
  * Displays an app-themed dark screen: *"Not Connected to the Internet — Click Reload when you Have Active Internet"*.
  * Features an emerald **Reload** button with automatic reconnect detection when network connectivity returns.
* **Instant 0 ms Pre-Flight Check**: Checks network interface availability before navigating, eliminating browser connection timeouts when offline.

### 🔕 4. Smart Active-Account-Only Notifications
* **Zero Background Interruptions**:
  * Audio chimes, Windows desktop toasts, taskbar flashing, and badge increments trigger **strictly for the active account**.
  * Inactive background accounts are automatically muted and have their web notification APIs silenced.

### 🖥️ 5. Native Windows Desktop Integration
* **Clean Lifecycle Management**:
  * Minimize (`[-]`) minimizes normally to the taskbar.
  * Close (`[X]`) hides silently to the System Tray to keep receiving messages in the background.
  * Left-click or double-click the system tray icon to restore the window instantly.
* **Dynamic Badges**: Real-time unread message counts are overlaid directly onto both the Taskbar icon and System Tray icon.
* **Automatic Memory Optimizer**: Trims the application's working set memory whenever minimized to tray.

### 🛡️ 6. 100% Ban-Safe & Clean-Safe Architecture
* **Zero Ban Risk**: Connects directly to the official `https://web.whatsapp.com/` endpoint using Microsoft's official Chromium engine with standard Edge User-Agents. No reverse-engineered APIs or modified APKs.
* **Cleaner Protected**: Session logins are stored safely in `%APPDATA%\WhatsLite\`, which is whitelisted by Windows Disk Cleanup and third-party cleaners (CCleaner, BleachBit).

### ℹ️ 7. In-App About Screen & Credits
* Accessible directly from the menu below account management.
* Displays Creator & Developer attribution: **Mr. Dipendu Das**.
* Features detailed technical specifications and a convenient **`[ ← Back ]`** button to return cleanly to chats.

---

## 📦 Downloads & Installation

| Package | Size | Description |
|---|---|---|
| **[WhatsLiteSetup.exe]** | **~385 KB** | **Recommended.** Standalone wizard installer. Creates Desktop & Start Menu shortcuts, and registers in Windows Settings > Apps with a clean uninstaller. |


---

## 🚀 Release History & Changelog

### 🌟 v1.3
