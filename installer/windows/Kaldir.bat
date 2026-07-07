@echo off
chcp 65001 >nul
title dockertest Agent Kaldir
cd /d "%~dp0"

net session >nul 2>&1
if %errorlevel% neq 0 (
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

for /f "usebackq delims=" %%i in (`powershell -NoProfile -Command "(Get-Content '%~dp0config.json' | ConvertFrom-Json).AgentContainerName"`) do set CONTAINER=%%i
if "%CONTAINER%"=="" set CONTAINER=dockertest-agent

echo Agent container kaldiriliyor: %CONTAINER%
docker rm -f %CONTAINER% 2>nul
if %errorlevel% equ 0 (
    echo [OK] Agent kaldirildi.
) else (
    echo Agent container bulunamadi veya Docker kapali.
)

pause
