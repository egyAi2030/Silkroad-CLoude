using System;
using System.Diagnostics;
using System.IO;

namespace SilkroadAIBot.Core.Helpers
{
    public static class ClientLauncher
    {
        public static bool Launch(string clientFolderPath, int locale = 22)
        {
            return LaunchRedirected(clientFolderPath, null, null, locale);
        }

        public static bool LaunchRedirected(string clientFolderPath, string? originalIp, string? redirectIp, int locale = 22)
        {
            string clientPath = Path.Combine(clientFolderPath, "sro_client.exe");

            if (!File.Exists(clientPath))
            {
                BotLogger.Error($"SRO_Client.exe not found at: {clientPath}");
                return false;
            }

            if (string.IsNullOrEmpty(originalIp) || string.IsNullOrEmpty(redirectIp))
            {
                try
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo
                    {
                        FileName = clientPath,
                        WorkingDirectory = clientFolderPath,
                        Arguments = $"/{locale} 0 0",
                        UseShellExecute = true,
                        Verb = "runas" 
                    };
                    BotLogger.Info($"Launching client (Standard): {clientPath}");
                    Process.Start(startInfo);
                    return true;
                }
                catch (Exception ex)
                {
                    BotLogger.Error("Failed to launch Silkroad client.", ex);
                    return false;
                }
            }
            else
            {
                // Use SmartLoader for direct memory patching (Stable, no external DLLs needed)
                BotLogger.Info($"Launching client via SmartLoader Memory Patching");
                return SmartLoader.LaunchAndPatch(clientPath, originalIp, redirectIp, locale);
            }
        }
    }
}
