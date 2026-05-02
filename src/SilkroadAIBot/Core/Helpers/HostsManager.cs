using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SilkroadAIBot.Core.Helpers
{
    public static class HostsManager
    {
        private static readonly string HostsPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "drivers/etc/hosts");
        private const string Marker = "# SilkroadAIBot Redirect";

        public static bool AddRedirect(string domain, string ip)
        {
            try
            {
                if (string.IsNullOrEmpty(domain) || domain == "127.0.0.1") return false;

                List<string> lines = File.ReadAllLines(HostsPath).ToList();
                string newLine = $"{ip}\t{domain}\t{Marker}";

                // Clean existing same domain entry
                lines.RemoveAll(l => l.Contains(domain) && l.Contains(Marker));
                
                lines.Add(newLine);
                File.WriteAllLines(HostsPath, lines);
                
                BotLogger.Info($"[Hosts] Added redirection: {domain} -> {ip}");
                return true;
            }
            catch (Exception ex)
            {
                BotLogger.Error($"[Hosts] Permission Denied: Could not update hosts file. Run as Administrator! Error: {ex.Message}");
                return false;
            }
        }

        public static void RemoveRedirect(string domain)
        {
            try
            {
                if (!File.Exists(HostsPath)) return;
                
                List<string> lines = File.ReadAllLines(HostsPath).ToList();
                int count = lines.RemoveAll(l => l.Contains(domain) && l.Contains(Marker));
                
                if (count > 0)
                {
                    File.WriteAllLines(HostsPath, lines);
                    BotLogger.Info($"[Hosts] Removed redirection for {domain}");
                }
            }
            catch { }
        }

        public static void CleanupAll()
        {
            try
            {
                if (!File.Exists(HostsPath)) return;
                
                List<string> lines = File.ReadAllLines(HostsPath).ToList();
                int count = lines.RemoveAll(l => l.Contains(Marker));
                
                if (count > 0)
                {
                    File.WriteAllLines(HostsPath, lines);
                    BotLogger.Info("[Hosts] All temporary redirections cleaned up.");
                }
            }
            catch { }
        }
    }
}
