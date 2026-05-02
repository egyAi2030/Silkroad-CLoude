using System;
using System.Collections.Generic;

namespace SilkroadAIBot.Core.Configuration
{
    public class BotConfig
    {
        public string SroPath { get; set; } = "";
        public string LastServerIP { get; set; } = "127.0.0.1";
        public int LastServerPort { get; set; } = 15779;
        public string Username { get; set; } = "";
        public string Password { get; set; } = ""; // Note: Should be encrypted in production
        public bool AutoLogin { get; set; } = false;
        public string AutoCharName { get; set; } = "";
        public string OriginalServerIp { get; set; } = ""; // IP to replace in Client memory
        public bool UseProxy { get; set; } = true;
        public int ProxyPort { get; set; } = 15777; // Local listening port
        
        // Cache for SmartLoader memory patch offsets
        public List<long> CachedIpOffsets { get; set; } = new List<long>();

        // Load/Save window positions or other UI state if needed
    }
}
