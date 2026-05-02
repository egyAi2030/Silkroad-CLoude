using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using SilkroadAIBot.Core.Configuration;

namespace SilkroadAIBot.Core.Helpers
{
    public static class SmartLoader
    {
        // Windows API constants and structs
        private const uint CREATE_SUSPENDED = 0x00000004;
        private const int MEM_COMMIT = 0x00001000;
        private const int PAGE_READWRITE = 0x04;

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

        [StructLayout(LayoutKind.Sequential)]
        public struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct SYSTEM_INFO
        {
            public uint dwOemId;
            public uint dwPageSize;
            public IntPtr minimumApplicationAddress;
            public IntPtr maximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
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

        [DllImport("kernel32.dll")]
        static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out int lpNumberOfBytesRead);

        [DllImport("kernel32.dll")]
        static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out int lpNumberOfBytesWritten);

        [DllImport("kernel32.dll")]
        static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll")]
        static extern void GetSystemInfo(ref SYSTEM_INFO lpSystemInfo);

        [DllImport("kernel32.dll")]
        static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, UIntPtr dwSize, uint flNewProtect, out uint lpflOldProtect);


        public static bool LaunchAndPatch(string clientPath, string originalIp, string redirectUrl, int locale = 22)
        {
            STARTUPINFO si = new STARTUPINFO();
            PROCESS_INFORMATION pi = new PROCESS_INFORMATION();
            si.cb = Marshal.SizeOf(si);

            string commandLine = $"\"{clientPath}\" /{locale} 0 0";
            string workingDir = System.IO.Path.GetDirectoryName(clientPath);

            BotLogger.Info($"[Loader] Creating process suspended: {clientPath}");

            if (CreateProcess(null, commandLine, IntPtr.Zero, IntPtr.Zero, false, CREATE_SUSPENDED, IntPtr.Zero, workingDir, ref si, out pi))
            {
                try
                {
                    BotLogger.Info($"[Loader] Process created (PID: {pi.dwProcessId}). Initiating Smart Memory Patcher...");

                    UniversalPatch(pi, originalIp, redirectUrl);

                    BotLogger.Info("[Loader] Calling ResumeThread. Client booting...");
                    ResumeThread(pi.hThread);
                    return true;
                }
                catch (Exception ex)
                {
                    BotLogger.Error($"[Loader] Error during patching: {ex.Message}");
                    return false;
                }
                finally
                {
                    CloseHandle(pi.hProcess);
                    CloseHandle(pi.hThread);
                }
            }
            else
            {
                BotLogger.Error($"[Loader] Failed to create process. Error: {Marshal.GetLastWin32Error()}");
                return false;
            }
        }

        private static void UniversalPatch(PROCESS_INFORMATION pi, string originalIp, string redirectUrl)
        {
            IntPtr hProcess = pi.hProcess;
            string targetIp = redirectUrl;
            ushort targetPort = 0;
            ushort originalPort = (ushort)ConfigManager.Config.LastServerPort;
            
            if (redirectUrl.Contains(":"))
            {
                var parts = redirectUrl.Split(':');
                targetIp = parts[0];
                if (ushort.TryParse(parts[1], out ushort p))
                    targetPort = p;
            }

            byte[] targetBytes = Encoding.ASCII.GetBytes(targetIp);
            
            // Append a null terminator so the client reads it properly
            byte[] patchBytes = new byte[targetBytes.Length + 1];
            Array.Copy(targetBytes, patchBytes, targetBytes.Length);
            patchBytes[patchBytes.Length - 1] = 0x00;

            // Search for IP in memory
            
            List<long> cachedOffsets = ConfigManager.Config.CachedIpOffsets ?? new List<long>();
            bool fastPathSuccess = false;

            // Step A & B: Try Fast Path
            if (cachedOffsets.Count > 0)
            {
                BotLogger.Info("[Scanner] Checking cached offsets (Fast Path)...");
                int successCount = 0;

                foreach (long relativeOffset in cachedOffsets)
                {
                    IntPtr targetAddress = (IntPtr)relativeOffset;
                    
                    // Verify if it contains an IP pattern
                    byte[] checkBuf = new byte[30];
                    if (ReadProcessMemory(hProcess, targetAddress, checkBuf, 30, out int bytesRead))
                    {
                        string strVal = Encoding.ASCII.GetString(checkBuf);
                        int nullIdx = strVal.IndexOf('\0');
                        if (nullIdx != -1) strVal = strVal.Substring(0, nullIdx);
                        
                        if (strVal == "255.255.255.0") continue;

                        if (strVal == originalIp)
                        {
                            BotLogger.Info($"[Scanner] Found TARGET IP at 0x{relativeOffset:X}");
                            WritePatch(hProcess, targetAddress, patchBytes, strVal.Length, false);
                            successCount++;
                            PatchPortNearAddress(hProcess, targetAddress, originalPort, targetPort);
                        }
                        else
                        {
                            // Could fail if memory is scrambled or if it was Unicode. Try simple Unicode check.
                            string uniVal = Encoding.Unicode.GetString(checkBuf);
                            int uniNullIdx = uniVal.IndexOf('\0');
                            if (uniNullIdx != -1) uniVal = uniVal.Substring(0, uniNullIdx);
                            
                            if (uniVal == "255.255.255.0") continue;

                            if (uniVal == originalIp)
                            {
                                BotLogger.Info($"[Scanner] Found TARGET IP at 0x{relativeOffset:X}");
                                WritePatch(hProcess, targetAddress, Encoding.Unicode.GetBytes(targetIp + "\0"), uniVal.Length * 2, true);
                                successCount++;
                                PatchPortNearAddress(hProcess, targetAddress, originalPort, targetPort);
                            }
                        }
                    }
                }

                if (successCount > 0)
                {
                    fastPathSuccess = true;
                }
                
                // Extra check for domain if it was not found in offsets
                if (!fastPathSuccess && !string.IsNullOrEmpty(originalIp))
                {
                    BotLogger.Info($"[Scanner] Fast path missed. Scanning specific pattern: {originalIp}");
                    // Implementation below in full scan
                }
            }

            // Step C: Full Scan
            if (!fastPathSuccess)
            {
                BotLogger.Info("[Scanner] Scanning memory for IP patterns...");
                List<long> newOffsets = new List<long>();
                
                SYSTEM_INFO sysInfo = new SYSTEM_INFO();
                GetSystemInfo(ref sysInfo);

                long currentAddress = (long)sysInfo.minimumApplicationAddress;
                long endAddress = (long)sysInfo.maximumApplicationAddress;

                MEMORY_BASIC_INFORMATION memInfo;
                
                byte[] asciiTarget = Encoding.ASCII.GetBytes(originalIp);
                byte[] unicodeTarget = Encoding.Unicode.GetBytes(originalIp);

                while (currentAddress < endAddress)
                {
                    VirtualQueryEx(hProcess, (IntPtr)currentAddress, out memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION)));

                    bool isScannable = memInfo.State == MEM_COMMIT && 
                                       (memInfo.Protect == PAGE_READWRITE || 
                                        memInfo.Protect == 0x40 /* PAGE_EXECUTE_READWRITE */ ||
                                        memInfo.Protect == 0x02 /* PAGE_READONLY */ ||
                                        memInfo.Protect == 0x20 /* PAGE_EXECUTE_READ */);

                    if (isScannable)
                    {
                        byte[] buffer = new byte[(int)memInfo.RegionSize];
                        if (ReadProcessMemory(hProcess, memInfo.BaseAddress, buffer, buffer.Length, out int bytesRead))
                        {
                            // Targeted sequence search for Domain/IP string (ASCII) - Patch ALL occurrences
                            int startSearch = 0;
                            while (true)
                            {
                                int pos = FindSequence(buffer, asciiTarget, startSearch, true); // Case-insensitive
                                if (pos == -1) break;

                                long offset = (long)memInfo.BaseAddress + pos;
                                BotLogger.Info($"[Scanner] Found TARGET (ASCII) at 0x{offset:X}. Patching...");
                                WritePatch(hProcess, (IntPtr)offset, patchBytes, originalIp.Length, false);
                                PatchPortNearAddress(hProcess, (IntPtr)offset, originalPort, targetPort);
                                newOffsets.Add(offset);
                                
                                startSearch = pos + asciiTarget.Length;
                                if (startSearch >= buffer.Length) break;
                            }

                            // Targeted sequence search for Domain/IP string (Unicode) - Patch ALL occurrences
                            startSearch = 0;
                            while (true)
                            {
                                int uPos = FindSequence(buffer, unicodeTarget, startSearch, true);
                                if (uPos == -1) break;

                                long offset = (long)memInfo.BaseAddress + uPos;
                                BotLogger.Info($"[Scanner] Found TARGET (Unicode) at 0x{offset:X}. Patching...");
                                byte[] uPatch = Encoding.Unicode.GetBytes(targetIp + "\0");
                                WritePatch(hProcess, (IntPtr)offset, uPatch, originalIp.Length * 2, true);
                                PatchPortNearAddress(hProcess, (IntPtr)offset, originalPort, targetPort);
                                newOffsets.Add(offset);

                                startSearch = uPos + unicodeTarget.Length;
                                if (startSearch >= buffer.Length) break;
                            }
                        }
                    }

                    long nextAddress = (long)memInfo.BaseAddress + (long)memInfo.RegionSize;
                    if (nextAddress <= currentAddress) break; 
                    currentAddress = nextAddress;
                }

                // Step D: Save Offsets
                if (newOffsets.Count > 0)
                {
                    ConfigManager.Config.CachedIpOffsets = newOffsets;
                    ConfigManager.Save();
                    BotLogger.Info($"[Config] Saved {newOffsets.Count} new Offset(s) to settings for future use.");
                }
                else
                {
                    BotLogger.Error("[Error] Scanner could not find any IP in memory!");
                    BotLogger.Warn("[Scanner] The client may be obfuscated or IP strings are extracted later.");
                }
            }
        }

        private static int FindSequence(byte[] buffer, byte[] pattern, int offset = 0, bool ignoreCase = false)
        {
            if (pattern.Length == 0) return -1;
            for (int i = offset; i <= buffer.Length - pattern.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < pattern.Length; j++)
                {
                    byte b1 = buffer[i + j];
                    byte b2 = pattern[j];

                    if (ignoreCase)
                    {
                        // ASCII case-insensitive check
                        if (b1 >= 'a' && b1 <= 'z') b1 = (byte)(b1 - 32);
                        if (b2 >= 'a' && b2 <= 'z') b2 = (byte)(b2 - 32);
                    }

                    if (b1 != b2)
                    {
                        match = false;
                        break;
                    }
                }
                if (match) return i;
            }
            return -1;
        }

        private static void WritePatch(IntPtr hProcess, IntPtr address, byte[] patchData, int originalByteLen, bool isUnicode = false)
        {
            uint oldProtect = 0;
            // Unprotect
            VirtualProtectEx(hProcess, address, (UIntPtr)originalByteLen, PAGE_READWRITE, out oldProtect);

            WriteProcessMemory(hProcess, address, patchData, patchData.Length, out int bytesWritten);

            // Verification Module Requested by User
            byte[] verifyBuf = new byte[patchData.Length];
            if (ReadProcessMemory(hProcess, address, verifyBuf, verifyBuf.Length, out int bytesRead))
            {
                bool match = true;
                for (int i = 0; i < patchData.Length; i++)
                {
                    if (verifyBuf[i] != patchData[i])
                    {
                        match = false;
                        break;
                    }
                }
                
                if (match)
                {
                    BotLogger.Info($"[Patcher] Successfully redirected to 127.0.0.1!");
                }
                else
                {
                    BotLogger.Error($"[Error] WriteProcessMemory FAILED at 0x{(long)address:X}. Data mismatch (Possible Permission Issue).");
                }
            }
            else
            {
                BotLogger.Error($"[Error] WriteProcessMemory FAILED at 0x{(long)address:X}. Read verification failed (Possible Permission Issue).");
            }

            // Restore protection
            VirtualProtectEx(hProcess, address, (UIntPtr)originalByteLen, oldProtect, out oldProtect);
        }

        private static void PatchPortNearAddress(IntPtr hProcess, IntPtr ipAddress, ushort originalPort, ushort targetPort)
        {
            if (originalPort == 0 || targetPort == 0 || originalPort == targetPort) return;

            // Scan 256 bytes before and 256 bytes after the IP address string
            long searchStart = (long)ipAddress - 256;
            int searchSize = 512 + 64; // 256 before + ip buffer padding + 256 after
            
            if (searchStart < 0) searchStart = (long)ipAddress;

            byte[] buffer = new byte[searchSize];
            if (ReadProcessMemory(hProcess, (IntPtr)searchStart, buffer, searchSize, out int bytesRead))
            {
                byte[] origPortLE = BitConverter.GetBytes(originalPort);
                byte[] origPortBE = new byte[] { (byte)(originalPort >> 8), (byte)(originalPort & 0xFF) };
                
                for (int i = 0; i < bytesRead - 1; i++)
                {
                    bool isLE = (buffer[i] == origPortLE[0] && buffer[i+1] == origPortLE[1]);
                    bool isBE = (buffer[i] == origPortBE[0] && buffer[i+1] == origPortBE[1]);

                    if (isLE || isBE)
                    {
                        IntPtr portAddress = (IntPtr)(searchStart + i);
                        byte[] newPortBytes = isLE ? BitConverter.GetBytes(targetPort) : new byte[] { (byte)(targetPort >> 8), (byte)(targetPort & 0xFF) };
                        
                        uint oldProtect = 0;
                        VirtualProtectEx(hProcess, portAddress, (UIntPtr)2, PAGE_READWRITE, out oldProtect);
                        WriteProcessMemory(hProcess, portAddress, newPortBytes, 2, out _);
                        VirtualProtectEx(hProcess, portAddress, (UIntPtr)2, oldProtect, out oldProtect);
                        
                        BotLogger.Info($"[Patcher] Successfully patched {(isLE ? "LE" : "BE")} Port {originalPort} -> {targetPort} at 0x{(long)portAddress:X}");
                    }
                }
            }
        }
        public static void HardPatchIP(IntPtr hProcess)
        {
            BotLogger.Info("[Patcher] Initiating Hard Memory Patch (Raw Byte Scanning)...");

            string targetIpStr = "192.168.100.9";
            string redirectIpStr = "127.0.0.1";
            ushort targetPort = 15779;
            ushort redirectPort = 15884;

            byte[] asciiTarget = Encoding.ASCII.GetBytes(targetIpStr);
            byte[] asciiRedirect = Encoding.ASCII.GetBytes(redirectIpStr + "\0"); // Null terminate for safety

            byte[] unicodeTarget = Encoding.Unicode.GetBytes(targetIpStr);
            byte[] unicodeRedirect = Encoding.Unicode.GetBytes(redirectIpStr + "\0");

            byte[] hexIpTarget = new byte[] { 192, 168, 100, 9 };
            byte[] hexIpRedirect = new byte[] { 127, 0, 0, 1 };

            byte[] hexPortTarget = BitConverter.GetBytes(targetPort);
            byte[] hexPortRedirect = BitConverter.GetBytes(redirectPort);

            SYSTEM_INFO sysInfo = new SYSTEM_INFO();
            GetSystemInfo(ref sysInfo);

            long currentAddress = (long)sysInfo.minimumApplicationAddress;
            long endAddress = (long)sysInfo.maximumApplicationAddress;

            MEMORY_BASIC_INFORMATION memInfo;
            int patchCount = 0;

            while (currentAddress < endAddress)
            {
                if (VirtualQueryEx(hProcess, (IntPtr)currentAddress, out memInfo, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) == 0)
                    break;

                bool isScannable = memInfo.State == MEM_COMMIT &&
                                   (memInfo.Protect == PAGE_READWRITE ||
                                    memInfo.Protect == 0x40 ||
                                    memInfo.Protect == 0x02 ||
                                    memInfo.Protect == 0x20);

                if (isScannable)
                {
                    byte[] buffer = new byte[(int)memInfo.RegionSize];
                    if (ReadProcessMemory(hProcess, memInfo.BaseAddress, buffer, buffer.Length, out int bytesRead))
                    {
                        // 1. ASCII Scan
                        patchCount += ReplaceInPool(hProcess, memInfo.BaseAddress, buffer, asciiTarget, asciiRedirect, "ASCII IP");
                        
                        // 2. Unicode Scan
                        patchCount += ReplaceInPool(hProcess, memInfo.BaseAddress, buffer, unicodeTarget, unicodeRedirect, "Unicode IP");

                        // 3. Hex IP Scan
                        patchCount += ReplaceInPool(hProcess, memInfo.BaseAddress, buffer, hexIpTarget, hexIpRedirect, "Hex IP");

                        // 4. Hex Port Scan
                        patchCount += ReplaceInPool(hProcess, memInfo.BaseAddress, buffer, hexPortTarget, hexPortRedirect, "Hex Port");
                    }
                }
                currentAddress += (long)memInfo.RegionSize;
            }

            BotLogger.Info($"[Patcher] Hard Patching completed. Total replacements: {patchCount}");
        }

        private static int ReplaceInPool(IntPtr hProcess, IntPtr baseAddress, byte[] buffer, byte[] target, byte[] replacement, string label)
        {
            int foundCount = 0;
            if (target.Length == 0) return 0;

            for (int i = 0; i <= buffer.Length - target.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < target.Length; j++)
                {
                    if (buffer[i + j] != target[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    IntPtr targetAddress = (IntPtr)((long)baseAddress + i);
                    
                    // If replacement is shorter than target, we want to clear the original area completely
                    // to prevent "127.0.0.1.100.9" style artifacts if the client reads by length.
                    byte[] thoroughReplacement = replacement;
                    if (replacement.Length < target.Length)
                    {
                        thoroughReplacement = new byte[target.Length];
                        Array.Copy(replacement, thoroughReplacement, replacement.Length);
                        // Rest of thoroughReplacement is already 0x00 by default (null bytes)
                    }

                    uint oldProtect;
                    VirtualProtectEx(hProcess, targetAddress, (UIntPtr)thoroughReplacement.Length, PAGE_READWRITE, out oldProtect);
                    
                    if (WriteProcessMemory(hProcess, targetAddress, thoroughReplacement, (int)thoroughReplacement.Length, out _))
                    {
                        BotLogger.Info($"[Patcher] Hard Patched {label} at 0x{(long)targetAddress:X} (Erased full {target.Length} bytes)");
                        foundCount++;
                    }
                    
                    VirtualProtectEx(hProcess, targetAddress, (UIntPtr)thoroughReplacement.Length, oldProtect, out oldProtect);
                    i += target.Length - 1;
                }
            }
            return foundCount;
        }
    }
}
