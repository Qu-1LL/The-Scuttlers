@echo off
setlocal
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0tools\push-dist.ps1" %*
exit /b %ERRORLEVEL%
