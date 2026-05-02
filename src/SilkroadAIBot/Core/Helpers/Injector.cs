using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SilkroadAIBot.Core.Helpers
{
    public static class Injector
    {
        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(int dwDesiredAccess, bool bInheritHandle, int dwProcessId);

        [DllImport("kernel32.dll", CharSet = CharSet.Auto)]
        public static extern IntPtr GetModuleHandle(string lpModuleName);

        [DllImport("kernel32", CharSet = CharSet.Ansi, ExactSpelling = true, SetLastError = true)]
        static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
        static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, uint nSize, out UIntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, IntPtr lpThreadId);

        // privileges
        const int PROCESS_CREATE_THREAD = 0x0002;
        const int PROCESS_QUERY_INFORMATION = 0x0400;
        const int PROCESS_VM_OPERATION = 0x0008;
        const int PROCESS_VM_WRITE = 0x0020;
        const int PROCESS_VM_READ = 0x0010;

        // used for memory allocation
        const uint MEM_COMMIT = 0x00001000;
        const uint MEM_RESERVE = 0x00002000;
        const uint PAGE_READWRITE = 0x04;

        [StructLayout(LayoutKind.Sequential)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll")]
        static extern uint ResumeThread(IntPtr hThread);

        [DllImport("kernel32.dll")]
        static extern bool CloseHandle(IntPtr hObject);

        const uint CREATE_SUSPENDED = 0x00000004;

        public static bool LaunchAndInject(string executablePath, string dllPath, int locale = 22)
        {
            STARTUPINFO si = new STARTUPINFO();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            si.cb = Marshal.SizeOf(si);

            string commandLine = $"\"{executablePath}\" /{locale} 0 0";
            string workingDir = System.IO.Path.GetDirectoryName(executablePath) ?? string.Empty;

            BotLogger.Info($"[Injector] Creating process suspended: {executablePath}");

            if (CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, workingDir, ref si, out pi))
            {
                try
                {
                    BotLogger.Info($"[Injector] Process created (PID: {pi.dwProcessId}). Injecting DLL...");
                    
                    if (Inject(pi.dwProcessId, dllPath))
                    {
                        // Injection succeeded. DLL will handle redirection.

                        BotLogger.Info("[Injector] Resuming thread...");
                        ResumeThread(pi.hThread);
                        return true;
                    }
                    else
                    {
                        BotLogger.Error("[Injector] Injection failed. Terminating process.");
                        Process.GetProcessById(pi.dwProcessId).Kill();
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    BotLogger.Error($"[Injector] Launch Error: {ex.Message}");
                    return false;
                }
                finally
                {
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                }
            }
            
            BotLogger.Error($"[Injector] Failed to create process '{executablePath}'. Error: {Marshal.GetLastWin32Error()}");
            return false;
        }

        public static bool Inject(int processId, string dllPath)
        {
            IntPtr procHandle = OpenProcess(PROCESS_CREATE_THREAD | PROCESS_QUERY_INFORMATION | PROCESS_VM_OPERATION | PROCESS_VM_WRITE | PROCESS_VM_READ, false, processId);
            if (procHandle == IntPtr.Zero)
            {
                BotLogger.Error($"[Injector] Could not open process {processId}. Access Denied.");
                return false;
            }

            IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
            if (loadLibraryAddr == IntPtr.Zero)
            {
                BotLogger.Error("[Injector] Could not get LoadLibraryA address.");
                return false;
            }

            IntPtr allocMemAddress = VirtualAllocEx(procHandle, IntPtr.Zero, (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char))), MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE);
            if (allocMemAddress == IntPtr.Zero)
            {
                BotLogger.Error("[Injector] VirtualAllocEx failed.");
                return false;
            }

            byte[] bytes = Encoding.Default.GetBytes(dllPath);
            UIntPtr bytesWritten;
            WriteProcessMemory(procHandle, allocMemAddress, bytes, (uint)bytes.Length, out bytesWritten);

            IntPtr threadHandle = CreateRemoteThread(procHandle, IntPtr.Zero, 0, loadLibraryAddr, allocMemAddress, 0, IntPtr.Zero);
            if (threadHandle == IntPtr.Zero)
            {
                BotLogger.Error("[Injector] CreateRemoteThread failed. Could not inject DLL!");
                return false;
            }

            BotLogger.Info($"[Injector] DLL injected successfully into PID: {processId}. Initialized at: 0x{allocMemAddress:X}");
            return true;
        }
    }
}
