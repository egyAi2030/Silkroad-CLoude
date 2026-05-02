using System;
using System.Diagnostics;
using System.Threading;
using System.Runtime.InteropServices;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Core.Memory
{
    public class SroLoader
    {
        // ... previous consts ...
        private const string SIG_NODC = "E8 ?? ?? ?? ?? 8B 4C 24 20 E8 ?? ?? ?? ?? 8B 4C 24 20";
        private const string SIG_MULTICLIENT = "6A 00 68 ?? ?? ?? ?? E8 ?? ?? ?? ?? 8B 44 24 10";

        public static Process? LaunchAndPatch(string clientPath, string gatewayIP, int targetPort, int proxyPort, bool enableNoDc)
        {
            BotLogger.Info("Loader", $"Starting SRO Client: {clientPath}");
            
            string clientDir = System.IO.Path.GetDirectoryName(clientPath)!;
            string dllSource = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Redirector.dll");
            string dllTarget = System.IO.Path.Combine(clientDir, "Redirector.dll");
            
            if (!System.IO.File.Exists(dllSource))
            {
                BotLogger.Error("Loader", $"CRITICAL ERROR: 'Redirector.dll' not found in bot folder ({dllSource}).");
                return null;
            }

            try
            {
                string configPath = System.IO.Path.Combine(clientDir, "proxy_config.ini");
                string configContent = $"[Proxy]\nTargetPort={targetPort}\nProxyPort={proxyPort}\nProxyIP=127.0.0.1\n";
                System.IO.File.WriteAllText(configPath, configContent);
                BotLogger.Info("Loader", $"Proxy config written to: {System.IO.Path.GetFullPath(configPath)} (Redirecting to localhost:{proxyPort})");
                
                System.IO.File.Copy(dllSource, dllTarget, true);
                BotLogger.Info("Loader", $"Redirector DLL deployed: {System.IO.Path.GetFullPath(dllTarget)}");
            }
            catch (Exception ex)
            {
                BotLogger.Error("Loader", $"Deployment failed: {ex.Message}");
                return null;
            }

            ProcessStartInfo startInfo = new ProcessStartInfo(clientPath);
            startInfo.WorkingDirectory = clientDir;
            
            try
            {
                System.Diagnostics.Process sroProcess = System.Diagnostics.Process.Start(startInfo)!;
                if (sroProcess == null) return null;

                BotLogger.Info("Loader", "Waiting for client initialization (800ms)...");
                Thread.Sleep(800);

                using (ProcessMemory memory = new ProcessMemory(sroProcess))
                {
                    BotLogger.Info("Loader", "Injecting Redirection DLL...");
                    if (InjectDll(sroProcess, dllTarget))
                    {
                        BotLogger.Info("Loader", "DLL Redirection active. Gateway will be hijacked.");
                    }
                    else
                    {
                        BotLogger.Error("Loader", "DLL Injection failed.");
                    }

                    if (enableNoDc)
                    {
                        AobScanner scanner = new AobScanner(sroProcess, memory);
                        BotLogger.Info("Loader", "Applying No-DC patch...");
                        IntPtr noDcAddr = scanner.FindSignature(SIG_NODC);
                        if (noDcAddr != IntPtr.Zero)
                        {
                            memory.Write(noDcAddr, new byte[] { 0x90, 0x90, 0x90, 0x90, 0x90 });
                            BotLogger.Info("Loader", "No-DC patch applied successfully.");
                        }
                    }

                    BotLogger.Info("Loader", "Client initialization completed. Enjoy!");
                    return sroProcess;
                }
            }
            catch (Exception ex)
            {
                BotLogger.Error("Loader", $"Failed to launch client: {ex.Message}");
                return null;
            }
        }
        
        public static void RevertDnsRedirect(string gatewayIP)
        {
            try
            {
                string hostsPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers\\etc\\hosts");
                string hostsContent = System.IO.File.ReadAllText(hostsPath);
                
                var lines = hostsContent.Split('\n').Where(l => !string.IsNullOrWhiteSpace(l) && !l.Contains(gatewayIP)).ToList();
                System.IO.File.WriteAllLines(hostsPath, lines);
                BotLogger.Info("Loader", $"DNS Redirect removed for {gatewayIP}.");
            }
            catch (Exception ex)
            {
                BotLogger.Error("Loader", $"Failed to revert DNS redirection: {ex.Message}");
            }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, byte[] lpBuffer, int dwSize, out IntPtr lpNumberOfBytesWritten);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, out IntPtr lpThreadId);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetModuleHandleA(string lpModuleName);

        private static bool InjectDll(System.Diagnostics.Process process, string dllPath)
        {
            try
            {
                IntPtr hProcess = process.Handle;
                IntPtr loadLibraryAddr = GetProcAddress(GetModuleHandleA("kernel32.dll"), "LoadLibraryA");
                if (loadLibraryAddr == IntPtr.Zero) return false;

                uint size = (uint)((dllPath.Length + 1) * Marshal.SizeOf(typeof(char)));
                IntPtr allocAddr = VirtualAllocEx(hProcess, IntPtr.Zero, size, 0x1000 | 0x2000, 0x04); // MEM_COMMIT | MEM_RESERVE, PAGE_READWRITE
                if (allocAddr == IntPtr.Zero) return false;

                byte[] dllBytes = System.Text.Encoding.ASCII.GetBytes(dllPath + "\0");
                IntPtr bytesWritten;
                if (!WriteProcessMemory(hProcess, allocAddr, dllBytes, dllBytes.Length, out bytesWritten)) return false;

                IntPtr threadId;
                IntPtr hThread = CreateRemoteThread(hProcess, IntPtr.Zero, 0, loadLibraryAddr, allocAddr, 0, out threadId);
                if (hThread == IntPtr.Zero) return false;

                return true;
            }
            catch { return false; }
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
    }
}
