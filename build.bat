@echo off
setlocal
echo ========================================================
echo   WhatsLite - Lightweight Windows Desktop Client Build
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] C# compiler not found at %CSC%
    exit /b 1
)

if exist "WhatsLite_old.exe" del /q "WhatsLite_old.exe" 2>nul
if exist "WhatsLite.exe" ren "WhatsLite.exe" "WhatsLite_old.exe" 2>nul

echo Compiling WhatsLite.exe...
"%CSC%" /target:winexe /optimize+ /out:WhatsLite.exe /win32icon:app.ico /win32manifest:app.manifest /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll,Microsoft.Web.WebView2.Core.dll,Microsoft.Web.WebView2.WinForms.dll Program.cs MainForm.cs TaskbarHelper.cs TrayBadgeHelper.cs MemoryOptimizer.cs Settings.cs AccountManager.cs AccountNameDialog.cs DarkMenuRenderer.cs AboutForm.cs ThemeHelper.cs

if %ERRORLEVEL% EQU 0 (
    echo [SUCCESS] WhatsLite.exe successfully built!
    echo File size:
    for %%I in (WhatsLite.exe) do echo   %%~zI bytes
    del /q "WhatsLite_old.exe" 2>nul
) else (
    echo [FAILED] Compilation failed with error code %ERRORLEVEL%
    if exist "WhatsLite_old.exe" ren "WhatsLite_old.exe" "WhatsLite.exe" 2>nul
)

endlocal
