@echo off
setlocal

set "ROOT_DIR=%~dp0"
set "PROJECT_PATH=%ROOT_DIR%src\TriloGame.Game\TriloGame.Game.csproj"

if not exist "%PROJECT_PATH%" (
    echo Could not find project file:
    echo   %PROJECT_PATH%
    exit /b 1
)

pushd "%ROOT_DIR%" >nul

echo Restoring TriloGame.Game...
dotnet restore "%PROJECT_PATH%"
if errorlevel 1 (
    popd >nul
    exit /b 1
)

echo Building TriloGame.Game...
dotnet build "%PROJECT_PATH%" -c Debug --no-restore
if errorlevel 1 (
    popd >nul
    exit /b 1
)

echo Launching TriloGame.Game...
dotnet run --project "%PROJECT_PATH%" -c Debug --no-build %*
set "EXIT_CODE=%ERRORLEVEL%"

popd >nul
exit /b %EXIT_CODE%
