@echo off
setlocal
echo ========================================================
echo   WhatsLite - Standalone Installer Build System
echo ========================================================

set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe

if not exist "%CSC%" (
    echo [ERROR] C# compiler not found at %CSC%
    exit /b 1
)

cd /d "%~dp0"

echo [1/4] Building main application WhatsLite.exe...
call build.bat
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to build WhatsLite.exe
    exit /b 1
)

echo.
echo [2/4] Building uninstaller Uninstall.exe...
cd installer
"%CSC%" /target:winexe /optimize+ /out:Uninstall.exe /win32icon:"..\app.ico" /r:System.dll,System.Windows.Forms.dll Uninstall.cs
if %ERRORLEVEL% NEQ 0 (
    echo [ERROR] Failed to build Uninstall.exe
    exit /b 1
)

echo.
echo [3/4] Compressing payload assets...
"%CSC%" /target:exe /out:CompressPayload.exe /r:System.dll CompressPayload.cs
CompressPayload.exe
del /q CompressPayload.exe

echo.
echo [4/4] Compiling standalone installer WhatsLiteSetup.exe...
if exist "..\WhatsLiteSetup_old.exe" del /q "..\WhatsLiteSetup_old.exe" 2>nul
if exist "..\WhatsLiteSetup.exe" ren "..\WhatsLiteSetup.exe" "WhatsLiteSetup_old.exe" 2>nul

"%CSC%" /target:winexe /optimize+ /out:"..\WhatsLiteSetup.exe" /win32icon:"..\app.ico" /win32manifest:"..\app.manifest" /resource:WhatsLite.exe.gz /resource:Microsoft.Web.WebView2.Core.dll.gz /resource:Microsoft.Web.WebView2.WinForms.dll.gz /resource:WebView2Loader.dll.gz /resource:app.ico.gz /resource:Uninstall.exe.gz /r:System.dll,System.Windows.Forms.dll,System.Drawing.dll InstallerProgram.cs

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   [SUCCESS] WhatsLiteSetup.exe successfully created!
    echo ========================================================
    for %%I in ("..\WhatsLiteSetup.exe") do echo   Installer size: %%~zI bytes
    del /q "..\WhatsLiteSetup_old.exe" 2>nul
) else (
    echo [ERROR] Failed to compile WhatsLiteSetup.exe
    if exist "..\WhatsLiteSetup_old.exe" ren "..\WhatsLiteSetup_old.exe" "WhatsLiteSetup.exe" 2>nul
)

cd ..
endlocal
