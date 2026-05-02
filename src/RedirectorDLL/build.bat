@echo off
set "VCWS_PATH=C:\Program Files\Microsoft Visual Studio\18\Community\VC\Auxiliary\Build\vcvarsall.bat"
echo Calling vcvarsall.bat x86...
call "%VCWS_PATH%" x86

echo Compiling Redirector.dll...
cl.exe /LD /Fe:Redirector.dll dllmain.cpp /link /OUT:Redirector.dll user32.lib ws2_32.lib advapi32.lib

if %ERRORLEVEL% equ 0 (
    echo.
    echo [SUCCESS] Redirector.dll built successfully!
    echo Staging DLL to bot folder...
    copy /Y Redirector.dll "D:\Anti\src\SilkroadAIBot\bin\x86\Release\net8.0-windows\Redirector.dll"
) else (
    echo.
    echo [ERROR] Compilation failed.
)
pause
