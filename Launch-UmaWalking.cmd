@echo off
if not exist "%~dp0DesktopLogs" mkdir "%~dp0DesktopLogs"
start "" "%~dp0UmaDesktop.exe" -force-d3d11 -force-d3d11-bitblt-model -logFile "%~dp0DesktopLogs\Player.log"
