@echo off
set "VCWS_PATH=C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat"
if not exist "%VCWS_PATH%" set "VCWS_PATH=C:\Program Files (x86)\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat"
if not exist "%VCWS_PATH%" set "VCWS_PATH=C:\Program Files\Microsoft Visual Studio\2022\Community\VC\Auxiliary\Build\vcvarsall.bat"

echo Initializing VC++ environment...
call "%VCWS_PATH%" x86

echo Compiling Redirector.dll (Manual Hook version)...
cl.exe /LD /Fe:Redirector.dll dllmain.cpp /D "_CRT_SECURE_NO_WARNINGS" /link /OUT:Redirector.dll user32.lib ws2_32.lib advapi32.lib

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Redirector.dll built successfully!
    echo Staging DLL to bot source folder...
    copy /Y Redirector.dll "..\SilkroadAIBot\Redirector.dll"
) else (
    echo.
    echo [ERROR] Compilation failed.
)
pause
