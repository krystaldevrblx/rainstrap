@echo off
setlocal enabledelayedexpansion
title Rainstrap Setup
color 0B

echo.
echo  ==============================================
echo           RAINSTRAP v1.5.0 INSTALLER
echo  ==============================================
echo.

REM ── Detect architecture ──────────────────────────────
set "ARCH=x64"
if "%PROCESSOR_ARCHITECTURE%"=="ARM64" set "ARCH=arm64"
if "%PROCESSOR_ARCHITECTURE%"=="x86" set "ARCH=x86"
echo  [i] Detected architecture: %ARCH%
echo.

REM ── .NET 6.0 Runtime ─────────────────────────────────
echo  -----------------------------------------------
echo    Checking: .NET 6.0 Runtime
echo  -----------------------------------------------

set "DOTNET_OK=0"
dotnet --list-runtimes 2>nul | findstr /C:"Microsoft.NETCore.App 6.0" >nul 2>&1
if !errorlevel! equ 0 (
    set "DOTNET_OK=1"
    echo    [OK] Already installed
)

if "!DOTNET_OK!"=="0" (
    echo    [~] Not found - downloading...
    curl -L --progress-bar -o "%TEMP%\rainstrap_dotnet.exe" "https://download.visualstudio.microsoft.com/download/pr/8aa6db58-3f22-4da3-ab15-5b15c7d42f2e/8e577e5a57a2e7b73ee4c9c7a20a9db5/windowsdesktop-runtime-6.0.36-win-%ARCH%.exe"
    if !errorlevel! neq 0 (
        echo    [!] Download failed - install manually from:
        echo        https://dotnet.microsoft.com/download/dotnet/6.0
    ) else (
        echo    [~] Installing...
        start /wait "" "%TEMP%\rainstrap_dotnet.exe" /install /quiet /norestart
        del "%TEMP%\rainstrap_dotnet.exe" 2>nul
        echo    [OK] Installed
    )
)
echo.

REM ── VC++ Redistributable ─────────────────────────────
echo  -----------------------------------------------
echo    Checking: Visual C++ Redistributable
echo  -----------------------------------------------

set "VC_OK=0"
reg query "HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" /v Installed 2>nul >nul
if !errorlevel! equ 0 set "VC_OK=1"
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\VisualStudio\14.0\VC\Runtimes\x64" /v Installed 2>nul >nul
if !errorlevel! equ 0 set "VC_OK=1"
reg query "HKLM\SOFTWARE\Microsoft\VisualStudio\14.0\VC\Runtimes\x86" /v Installed 2>nul >nul
if !errorlevel! equ 0 set "VC_OK=1"

if "!VC_OK!"=="1" (
    echo    [OK] Already installed
)

if "!VC_OK!"=="0" (
    echo    [~] Not found - downloading...
    curl -L --progress-bar -o "%TEMP%\rainstrap_vc.exe" "https://aka.ms/vs/17/release/vc_redist.%ARCH%.exe"
    if !errorlevel! neq 0 (
        echo    [!] Download failed - install manually from:
        echo        https://aka.ms/vs/17/release/vc_redist.x64.exe
    ) else (
        echo    [~] Installing...
        start /wait "" "%TEMP%\rainstrap_vc.exe" /install /quiet /norestart
        del "%TEMP%\rainstrap_vc.exe" 2>nul
        echo    [OK] Installed
    )
)
echo.

REM ── WebView2 Runtime ─────────────────────────────────
echo  -----------------------------------------------
echo    Checking: Microsoft WebView2 Runtime
echo  -----------------------------------------------

set "WV_OK=0"
reg query "HKLM\SOFTWARE\WOW6432Node\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BEB-23A81953DB7D}" /v pv 2>nul >nul
if !errorlevel! equ 0 set "WV_OK=1"
reg query "HKLM\SOFTWARE\Microsoft\EdgeUpdate\Clients\{F3017226-FE2A-4295-8BEB-23A81953DB7D}" /v pv 2>nul >nul
if !errorlevel! equ 0 set "WV_OK=1"

if "!WV_OK!"=="1" (
    echo    [OK] Already installed
)

if "!WV_OK!"=="0" (
    echo    [~] Not found - downloading...
    curl -L --progress-bar -o "%TEMP%\rainstrap_wv.exe" "https://go.microsoft.com/fwlink/p/?LinkId=2124703"
    if !errorlevel! neq 0 (
        echo    [!] Download failed - install manually from:
        echo        https://developer.microsoft.com/en-us/microsoft-edge/webview2/
    ) else (
        echo    [~] Installing...
        start /wait "" "%TEMP%\rainstrap_wv.exe" /silent /install
        del "%TEMP%\rainstrap_wv.exe" 2>nul
        echo    [OK] Installed
    )
)

echo.
echo.
echo  ==============================================
echo            SETUP COMPLETE
echo  ==============================================
echo.
echo  Rainstrap is ready to use.
echo  Double-click Rainstrap.exe to launch.
echo.
pause
