@echo off
set LOG_FILE=build_log.txt
echo [%DATE% %TIME%] Starting Build... > %LOG_FILE%

:: Close running bot instance if any to avoid file locks
taskkill /f /im SilkroadAIBot.exe >nul 2>&1

:: Ensure we are in the correct directory
cd /d "%~dp0"

echo Building Silkroad AI Bot (Antigravity)...
dotnet build SilkroadAIBot.csproj -c Debug >> %LOG_FILE% 2>&1

if %ERRORLEVEL% EQU 0 (
    echo [%DATE% %TIME%] Build Successful! >> %LOG_FILE%
    echo.
    echo ========================================
    echo        BUILD SUCCESSFUL!
    echo ========================================
    echo.
) else (
    echo [%DATE% %TIME%] Build Failed with error %ERRORLEVEL% >> %LOG_FILE%
    echo.
    echo ========================================
    echo        BUILD FAILED!
    echo ========================================
    echo Check build_log.txt for detailed errors.
    echo.
)

pause
