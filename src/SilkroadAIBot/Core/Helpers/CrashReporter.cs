using System;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace SilkroadAIBot.Core.Helpers
{
    public static class CrashReporter
    {
        public static void Report(Exception? ex, string source)
        {
            try
            {
                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string logFile = $"crash_report_{timestamp}.txt";
                
                StringBuilder sb = new StringBuilder();
                sb.AppendLine("=== SILKROAD AI BOT CRASH REPORT ===");
                sb.AppendLine($"Timestamp: {DateTime.Now}");
                sb.AppendLine($"Source: {source}");
                sb.AppendLine($"OS Version: {Environment.OSVersion}");
                sb.AppendLine($"Runtime: {Environment.Version}");
                sb.AppendLine($"Working Dir: {Environment.CurrentDirectory}");
                sb.AppendLine("------------------------------------");
                
                if (ex != null)
                {
                    sb.AppendLine($"Exception: {ex.GetType().FullName}");
                    sb.AppendLine($"Message: {ex.Message}");
                    sb.AppendLine($"Stack Trace:\n{ex.StackTrace}");
                    
                    if (ex.InnerException != null)
                    {
                        sb.AppendLine("\n--- Inner Exception ---");
                        sb.AppendLine($"Message: {ex.InnerException.Message}");
                        sb.AppendLine($"Stack Trace:\n{ex.InnerException.StackTrace}");
                    }
                }
                else
                {
                    sb.AppendLine("No exception object provided.");
                }

                File.WriteAllText(logFile, sb.ToString());
                
                MessageBox.Show($"A critical error occurred. A crash report has been saved to:\n{Path.GetFullPath(logFile)}\n\nError: {(ex != null ? ex.Message : "Unknown")}", 
                    "Critical Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch
            {
                // Last resort
                MessageBox.Show("A critical error occurred and the crash reporter also failed to save the log.", "Fatal Error", MessageBoxButtons.OK, MessageBoxIcon.Stop);
            }
        }
    }
}
