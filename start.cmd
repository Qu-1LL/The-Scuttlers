@echo off
setlocal
call "%~dp0dotnet-launch.cmd" %*
exit /b %ERRORLEVEL%
