using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SilkroadAIBot.Core.Helpers
{
    public class EdxLoader
    {
        private string _clientPath;
        private bool _zoomHack;
        private bool _multiClient;
        private bool _swearFilter;

        public EdxLoader(string clientPath)
        {
            _clientPath = clientPath;
            // Default patches enabled
            _zoomHack = true;
            _multiClient = true;
            _swearFilter = true;
        }

        public void SetPatches(bool zoomHack, bool multiClient, bool swearFilter)
        {
            _zoomHack = zoomHack;
            _multiClient = multiClient;
            _swearFilter = swearFilter;
        }

        public bool StartClient(bool useRandomValue, byte locale, int division, int hostIndex, int redirectingPort, string redirectingHost = "127.0.0.1")
        {
            string loaderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xBotLoader.exe");
            string dllPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "xBotLoader.dll");

            if (!File.Exists(loaderPath) || !File.Exists(dllPath))
            {
                BotLogger.Error($"xBotLoader files not found at: {AppDomain.CurrentDomain.BaseDirectory}. Make sure xBotLoader.exe and xBotLoader.dll are copied to the output directory.");
                return false;
            }

            // Generate config used by the DLL
            CreateDLLSetup(redirectingHost, redirectingPort);

            // Execute EdxLoader
            Process loader = new Process();
            loader.StartInfo.FileName = loaderPath;
            loader.StartInfo.Arguments = (useRandomValue ? "--userandom " : "") + $"-locale {locale} -division {division} -host {hostIndex} -path \"{_clientPath}\"";
            loader.StartInfo.UseShellExecute = true;
            loader.StartInfo.Verb = "runas"; // Request Admin if needed

            try
            {
                BotLogger.Info($"[Loader] Injecting DLL via xBotLoader (Redirecting to {redirectingHost}:{redirectingPort})...");
                loader.Start();
                loader.WaitForExit();

                if (loader.ExitCode > 0)
                {
                    BotLogger.Info($"[Loader] Successfully launched and injected SRO Client (PID: {loader.ExitCode}).");
                    return true;
                }
                else
                {
                    BotLogger.Error("[Loader] Client launch failed or xBotLoader returned 0.");
                    return false;
                }
            }
            catch (Exception ex)
            {
                BotLogger.Error($"[Loader] Failed to execute xBotLoader: {ex.Message}");
                return false;
            }
        }

        private void CreateDLLSetup(string redirectingHost, int redirectingPort)
        {
            StringBuilder cfg = new StringBuilder();
            cfg.AppendLine("[Patches]");
            cfg.AppendLine("English_Patch=no");
            cfg.AppendLine("Multiclient=" + (_multiClient ? "yes" : "no"));
            cfg.AppendLine("Debug_Console=no");
            cfg.AppendLine("Swear_Filter=" + (_swearFilter ? "yes" : "no"));
            cfg.AppendLine("Nude_Patch=no");
            cfg.AppendLine("Zoom_Hack=" + (_zoomHack ? "yes" : "no"));
            cfg.AppendLine("Korean_Captcha=no");
            cfg.AppendLine("No_Hackshield=no");
            cfg.AppendLine("Redirect_Gateway=yes");
            cfg.AppendLine("Redirect_Agent=no");
            cfg.AppendLine($"Gateway_Ip={redirectingHost}");
            cfg.AppendLine($"Gateway_Port={redirectingPort}");
            cfg.AppendLine("Agent_Ip=127.0.0.1");
            cfg.AppendLine("Agent_Port=0");
            cfg.AppendLine("Hook_Input=no");
            cfg.AppendLine("Patch_Seed=no");
            cfg.AppendLine("Auto_Parse=no");
            cfg.AppendLine("KSRO_750=no");

            // Getting a private temporal app space
            string cfgPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "xBot");
            if (!Directory.Exists(cfgPath))
            {
                Directory.CreateDirectory(cfgPath);
            }
            
            cfgPath = Path.Combine(cfgPath, "xBotLoader.ini");
            File.WriteAllText(cfgPath, cfg.ToString());
        }
    }
}
