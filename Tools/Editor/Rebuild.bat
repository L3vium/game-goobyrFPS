@echo off
rem Double-click (or run) to compile the Hitmen C++: Live Coding when possible,
rem otherwise save + close the editor, build, and reopen it. Extra args go to ue_rebuild.ps1
rem (for example: Rebuild.bat -Mode full).
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0ue_rebuild.ps1" %*
set RESULT=%ERRORLEVEL%
if not defined HITMEN_NOPAUSE pause
exit /b %RESULT%
