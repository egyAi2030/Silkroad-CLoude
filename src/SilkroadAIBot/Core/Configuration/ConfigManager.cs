using System;
using System.IO;
using System.Text.Json; // Native JSON support in .NET Core / newer Frameworks
// If using older Framework, might need Newtonsoft, but let's try System.Text.Json

namespace SilkroadAIBot.Core.Configuration
{
    public static class ConfigManager
    {
        private static string _configPath = "bot_config.json";
        public static BotConfig Config { get; private set; } = new BotConfig();

        public static void Load()
        {
            try
            {
                if (File.Exists(_configPath))
                {
                    string json = File.ReadAllText(_configPath);
                    var loaded = System.Text.Json.JsonSerializer.Deserialize<BotConfig>(json);
                    if (loaded != null) Config = loaded;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Failed to load config: {ex.Message}");
            }
        }

        public static void Save()
        {
            try
            {
                var options = new System.Text.Json.JsonSerializerOptions { WriteIndented = true };
                string json = System.Text.Json.JsonSerializer.Serialize(Config, options);
                File.WriteAllText(_configPath, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ConfigManager] Failed to save config: {ex.Message}");
            }
        }
    }
}
