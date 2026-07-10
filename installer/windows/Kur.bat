@echo off
chcp 65001 >nul
title dockertest Agent Kurulumu
cd /d "%~dp0"

net session >nul 2>&1
if %errorlevel% neq 0 (
    echo Yonetici izni isteniyor...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo  dockertest Agent - Windows Kurulum
echo  -----------------------------------
echo.

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0install.ps1" %*
set EXITCODE=%ERRORLEVEL%

echo.
if %EXITCODE% equ 0 (
    echo [OK] Kurulum bitti.
) else (
    echo [HATA] Kurulum tamamlanamadi. Kod: %EXITCODE%
)

echo.
pause
exit /b %EXITCODE%
