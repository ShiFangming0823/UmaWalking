@echo off
if not exist "%~dp0DesktopLogs" mkdir "%~dp0DesktopLogs"
start "" "%~dp0UmaDesktop.exe" -force-d3d11 -force-d3d11-bitblt-model --desktop-fps 15 --desktop-render-scale 0.75 --desktop-max-actors 2 --desktop-msaa 2 -logFile "%~dp0DesktopLogs\Player.log"
