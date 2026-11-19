@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo       WhatsLite - One-Click GitHub Uploader
echo ========================================================
echo.

cd /d "%~dp0"

:: 1. Check if git is installed
where git >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [!] Git is not found on your system.
    echo [*] Installing Git automatically via Windows Package Manager (winget)...
    echo.
    winget install --id Git.Git -e --source winget --accept-source-agreements --accept-package-agreements
    if %ERRORLEVEL% NEQ 0 (
        echo.
        echo [ERROR] Automatic Git installation failed.
        echo Please download and install Git from: https://git-scm.com/download/win
        pause
        exit /b 1
    )
    echo [*] Refreshing environment variables...
    set "PATH=%PATH%;C:\Program Files\Git\cmd;C:\Program Files\Git\bin"
)

:: Verify git again
where git >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    if exist "C:\Program Files\Git\cmd\git.exe" (
        set "PATH=%PATH%;C:\Program Files\Git\cmd"
    ) else (
        echo [!] Git was installed, but please restart this terminal window to refresh your PATH.
        pause
        exit /b 0
    )
)

echo [OK] Git is ready!
echo.
echo Please create a new empty repository at: https://github.com/new
echo (Name it "WhatsLite", leave "Add README" unchecked)
echo.
set /p REPO_URL="Enter your GitHub repository URL (e.g. https://github.com/YourUsername/WhatsLite.git): "

if "%REPO_URL%"=="" (
    echo [ERROR] No URL entered. Aborting.
    pause
    exit /b 1
)

echo.
echo [*] Initializing Git repository...
if exist ".git" (
    echo [*] Existing .git found, updating remote...
    git remote remove origin >nul 2>&1
) else (
    git init
)

echo [*] Staging project files...
git add .

echo [*] Creating commit...
git commit -m "Initial release of WhatsLite: Lightweight Windows desktop client for WhatsApp Web"

echo [*] Setting branch to main...
git branch -M main

echo [*] Adding remote origin...
git remote add origin %REPO_URL%

echo [*] Pushing to GitHub...
git push -u origin main

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   [SUCCESS] WhatsLite has been pushed to GitHub!
    echo ========================================================
) else (
    echo.
    echo [!] Push completed with code %ERRORLEVEL%.
    echo If GitHub prompted for login, complete the sign-in prompt and try running this script again.
)

echo.
pause
endlocal
