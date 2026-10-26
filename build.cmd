@echo off
REM Rebuild HotspotLauncher.exe. All messages are printed by build.ps1.
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0build.ps1"
set RC=%ERRORLEVEL%
echo.
if not "%RC%"=="0" echo Build FAILED (exit %RC%). See messages above.
pause
exit /b %RC%
