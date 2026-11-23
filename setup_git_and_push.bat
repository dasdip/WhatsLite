@echo off
setlocal enabledelayedexpansion

echo ========================================================
echo       WhatsLite - One-Click GitHub Uploader
echo ========================================================
echo.

cd /d "%~dp0"

where git >nul 2>&1
if %ERRORLEVEL% NEQ 0 (
    echo [!] Git is not found on your system.
    echo Please download and install Git from: https://git-scm.com/download/win
    pause
    exit /b 1
)

echo [OK] Git is ready!
echo.
echo Step 1: Create a new repository at: https://github.com/new
echo         - Repository name: WhatsLite
echo         - Leave "Add a README file" UNCHECKED
echo.
set /p REPO_URL="Enter your GitHub repository URL (e.g. https://github.com/YourUsername/WhatsLite.git): "

if "%REPO_URL%"=="" (
    echo [ERROR] No URL entered. Aborting.
    pause
    exit /b 1
)

echo.
echo [*] Staging any updated files...
git add .

echo [*] Committing changes...
git commit -m "WhatsLite v1.3 - Lightweight Multi-Account Windows Desktop Client for WhatsApp Web" 2>nul

echo [*] Setting branch to main...
git branch -M main

echo [*] Setting remote origin...
git remote remove origin >nul 2>&1
git remote add origin %REPO_URL%

echo [*] Pushing to GitHub...
git push -u origin main

if %ERRORLEVEL% EQU 0 (
    echo.
    echo ========================================================
    echo   [SUCCESS] WhatsLite v1.3 has been pushed to GitHub!
    echo ========================================================
) else (
    echo.
    echo [!] If GitHub prompted for login, complete the sign-in window.
    echo If your repository was initialized with files, run:
    echo   git push -u origin main --force
)

echo.
pause
endlocal
