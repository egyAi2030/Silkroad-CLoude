using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SilkroadAIBot.Core.Memory
{
    public class ProcessMemory : IDisposable
    {
        [Flags]
        public enum ProcessAccessFlags : uint
        {
            All = 0x001F0FFF,
            Terminate = 0x00000001,
            CreateThread = 0x00000002,
            VirtualMemoryOperation = 0x00000008,
            VirtualMemoryRead = 0x00000010,
            VirtualMemoryWrite = 0x00000020,
            DuplicateHandle = 0x00000040,
            CreateProcess = 0x00000080,
            SetQuota = 0x00000100,
            SetInformation = 0x00000200,
            QueryInformation = 0x00000400,
            QueryLimitedInformation = 0x00001000,
            Synchronize = 0x00100000
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

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(ProcessAccessFlags processAccess, bool bInheritHandle, int processId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool ReadProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, [Out] byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesRead);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool VirtualProtectEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flNewProtect, out uint lpflOldProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(IntPtr hProcess, IntPtr lpAddress, out MEMORY_BASIC_INFORMATION lpBuffer, uint dwLength);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandleA(string lpModuleName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);

        [DllImport("ntdll.dll", PreserveSig = false)]
        public static extern void NtSuspendProcess(IntPtr processHandle);

        [DllImport("ntdll.dll", PreserveSig = false)]
        public static extern void NtResumeProcess(IntPtr processHandle);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr hObject);

        private IntPtr _processHandle = IntPtr.Zero;
        private System.Diagnostics.Process _process;
        
        public const uint MEM_COMMIT = 0x1000;
        public const uint PAGE_READWRITE = 0x04;
        public const uint PAGE_READONLY = 0x02;
        public const uint PAGE_EXECUTE_READWRITE = 0x40;

        public void Suspend() => NtSuspendProcess(_processHandle);
        public void Resume() => NtResumeProcess(_processHandle);

        public ProcessMemory(System.Diagnostics.Process process)
        {
            _process = process;
            _processHandle = OpenProcess(ProcessAccessFlags.All, false, process.Id);
            if (_processHandle == IntPtr.Zero)
                throw new Exception($"Could not open process {process.ProcessName} (ID: {process.Id}). Access Denied.");
        }

        public byte[] Read(IntPtr address, int size)
        {
            byte[] buffer = new byte[size];
            if (!ReadProcessMemory(_processHandle, address, buffer, size, out _))
                return null!;
            return buffer;
        }

        public bool Write(IntPtr address, byte[] data)
        {
            uint oldProtect;
            // Ensure we can write to the memory (e.g. if it's executable code section)
            VirtualProtectEx(_processHandle, address, (uint)data.Length, 0x40, out oldProtect); // PAGE_EXECUTE_READWRITE
            
            bool success = WriteProcessMemory(_processHandle, address, data, data.Length, out _);
            
            // Restore protection
            VirtualProtectEx(_processHandle, address, (uint)data.Length, oldProtect, out _);
            
            return success;
        }

        public bool WriteString(IntPtr address, string text, bool unicode = false)
        {
            byte[] data = unicode ? Encoding.Unicode.GetBytes(text + "\0") : Encoding.ASCII.GetBytes(text + "\0");
            return Write(address, data);
        }

        public System.Collections.Generic.List<MEMORY_BASIC_INFORMATION> GetReadableRegions()
        {
            var regions = new System.Collections.Generic.List<MEMORY_BASIC_INFORMATION>();
            IntPtr address = IntPtr.Zero;
            
            while (VirtualQueryEx(_processHandle, address, out MEMORY_BASIC_INFORMATION mbi, (uint)Marshal.SizeOf(typeof(MEMORY_BASIC_INFORMATION))) != 0)
            {
                // Only interested in committed memory that is readable
                bool isReadable = (mbi.State == MEM_COMMIT) && 
                                  ((mbi.Protect & PAGE_READONLY) != 0 || 
                                   (mbi.Protect & PAGE_READWRITE) != 0 || 
                                   (mbi.Protect & PAGE_EXECUTE_READWRITE) != 0);
                                   
                if (isReadable && mbi.RegionSize.ToInt64() > 0)
                {
                    regions.Add(mbi);
                }
                
                address = new IntPtr(mbi.BaseAddress.ToInt64() + mbi.RegionSize.ToInt64());
            }
            
            return regions;
        }

        public T ReadStruct<T>(IntPtr address) where T : struct
        {
            int size = Marshal.SizeOf(typeof(T));
            byte[] data = Read(address, size);
            if (data == null) return default;

            GCHandle handle = GCHandle.Alloc(data, GCHandleType.Pinned);
            try
            {
                return (T)Marshal.PtrToStructure(handle.AddrOfPinnedObject(), typeof(T));
            }
            finally
            {
                handle.Free();
            }
        }

        public IntPtr GetIatAddress(string dllName, string functionName)
        {
            // Implementation to walk the PE header and find the FirstThunk for the function
            // We scan the main module for imports
            ProcessModule mainModule = _process.MainModule;
            IntPtr baseAddr = mainModule.BaseAddress;

            var dosHeader = ReadStruct<PeHeaders.IMAGE_DOS_HEADER>(baseAddr);
            if (dosHeader.e_magic != 0x5A4D) return IntPtr.Zero; // 'MZ'

            IntPtr ntHeaderAddr = IntPtr.Add(baseAddr, dosHeader.e_lfanew);
            var ntHeaders = ReadStruct<PeHeaders.IMAGE_NT_HEADERS32>(ntHeaderAddr);
            if (ntHeaders.Signature != 0x4550) return IntPtr.Zero; // 'PE'

            // Import Directory is usually at index 1
            uint importRva = ntHeaders.OptionalHeader.DataDirectory[1].VirtualAddress;
            if (importRva == 0) return IntPtr.Zero;

            IntPtr importDescAddr = IntPtr.Add(baseAddr, (int)importRva);
            int descSize = Marshal.SizeOf(typeof(PeHeaders.IMAGE_IMPORT_DESCRIPTOR));

            while (true)
            {
                var importDesc = ReadStruct<PeHeaders.IMAGE_IMPORT_DESCRIPTOR>(importDescAddr);
                if (importDesc.Name == 0) break;

                string currentDll = ReadString(IntPtr.Add(baseAddr, (int)importDesc.Name));
                if (currentDll.Equals(dllName, StringComparison.OrdinalIgnoreCase))
                {
                    // Found the DLL! Now search for the function in the thunk list
                    IntPtr thunkAddr = IntPtr.Add(baseAddr, (int)importDesc.FirstThunk);
                    IntPtr originalThunkAddr = IntPtr.Add(baseAddr, (int)importDesc.OriginalFirstThunk);

                    int i = 0;
                    while (true)
                    {
                        uint funcRva = BitConverter.ToUInt32(Read(IntPtr.Add(originalThunkAddr, i * 4), 4), 0);
                        if (funcRva == 0) break;

                        // Check if it's imported by name
                        if ((funcRva & 0x80000000) == 0)
                        {
                            string currentFunc = ReadString(IntPtr.Add(baseAddr, (int)funcRva + 2));
                            if (currentFunc.Equals(functionName, StringComparison.OrdinalIgnoreCase))
                            {
                                // Return the address IN THE IAT where the function pointer is stored
                                return IntPtr.Add(thunkAddr, i * 4);
                            }
                        }
                        i++;
                    }
                }
                importDescAddr = IntPtr.Add(importDescAddr, descSize);
            }

            return IntPtr.Zero;
        }

        public string ReadString(IntPtr address, int length = 256)
        {
            byte[] buffer = Read(address, length);
            if (buffer == null) return string.Empty;
            int end = Array.IndexOf(buffer, (byte)0);
            if (end == -1) end = buffer.Length;
            return System.Text.Encoding.ASCII.GetString(buffer, 0, end);
        }

        public void Dispose()
        {
            if (_processHandle != IntPtr.Zero)
            {
                CloseHandle(_processHandle);
                _processHandle = IntPtr.Zero;
            }
        }
    }
}
