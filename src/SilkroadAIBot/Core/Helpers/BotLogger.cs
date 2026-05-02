using System.Collections.Concurrent;
using SilkroadAIBot.Core.Settings;

namespace SilkroadAIBot.Core.Helpers
{
    public static class BotLogger
    {
        public enum LogLevel { DEBUG = 0, INFO = 1, WARN = 2, ERROR = 3 }

        public static event Action<string, LogLevel>? OnLogMessage;
        
        private static string _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        private static BlockingCollection<string> _logQueue = new BlockingCollection<string>();
        private static CancellationTokenSource _cts = new CancellationTokenSource();
        private static Thread _workerThread;

        static BotLogger()
        {
            if (!Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            _workerThread = new Thread(ProcessLogs) { IsBackground = true, Name = "BotLoggerWorker" };
            _workerThread.Start();
        }

        public static void Info(string message) => Log(LogLevel.INFO, "General", message);
        public static void Info(string module, string message) => Log(LogLevel.INFO, module, message);
        
        public static void Debug(string message) => Log(LogLevel.DEBUG, "General", message);
        public static void Debug(string module, string message) => Log(LogLevel.DEBUG, module, message);
        
        public static void Warn(string message) => Log(LogLevel.WARN, "General", message);
        public static void Warn(string module, string message) => Log(LogLevel.WARN, module, message);

        public static void Error(string message, Exception? ex = null) => Error("General", message, ex);
        public static void Error(string module, string message, Exception? ex = null)
        {
            if (ex != null)
                Log(LogLevel.ERROR, module, $"{message} - {ex.Message}\n{ex.StackTrace}");
            else
                Log(LogLevel.ERROR, module, message);
        }

        private static void Log(LogLevel level, string module, string message)
        {
            // Filter by setting
            if ((int)level < BotSettings.Instance.MinLogLevel) return;

            string timeStamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            string formattedMessage = $"[{timeStamp}] [{level}] [{module}] {message}";

            // Target 1: Queue for File writing
            _logQueue.Add(formattedMessage);

            // Target 2: UI Event (Sync for direct display)
            OnLogMessage?.Invoke(formattedMessage, level);
            
            // Also output to Console so RedirectConsole can capture it as a secondary path
            if (level == LogLevel.ERROR) Console.Error.WriteLine(formattedMessage);
            else Console.WriteLine(formattedMessage);
        }

        private static void ProcessLogs()
        {
            while (!_cts.Token.IsCancellationRequested || _logQueue.Count > 0)
            {
                try
                {
                    if (_logQueue.TryTake(out string message, 100))
                    {
                        string fileName = $"bot_{DateTime.Now:yyyy-MM-dd}.txt";
                        string fullPath = Path.Combine(_logDirectory, fileName);
                        File.AppendAllText(fullPath, message + Environment.NewLine);
                    }
                }
                catch { /* Last resort catch to prevent thread crash */ }
            }
        }

        public static void Shutdown()
        {
            _cts.Cancel();
            _logQueue.CompleteAdding();
            _workerThread.Join(1000); // Wait up to 1s for remaining logs
        }
    }
}
