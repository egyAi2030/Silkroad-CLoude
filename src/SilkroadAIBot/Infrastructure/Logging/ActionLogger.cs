using System;
using System.Collections.Generic;
using System.Linq;
using SilkroadAIBot.Application.Interfaces;

namespace SilkroadAIBot.Infrastructure.Logging
{
    /// <summary>
    /// Implementation of IActionLogger using an in-memory queue.
    /// Thread-safe and bounded to prevent memory leaks.
    /// </summary>
    public class ActionLogger : IActionLogger
    {
        private readonly Queue<string> _logs = new();
        private readonly int _maxLogs;
        private readonly object _lock = new();

        public event Action<string>? OnLogAdded;

        public ActionLogger(int maxLogs = 500)
        {
            _maxLogs = maxLogs;
        }

        public void Log(string message)
        {
            string entry = $"[{DateTime.Now:HH:mm:ss}] {message}";
            
            lock (_lock)
            {
                _logs.Enqueue(entry);
                while (_logs.Count > _maxLogs)
                {
                    _logs.Dequeue();
                }
            }

            OnLogAdded?.Invoke(entry);
        }

        public IReadOnlyList<string> GetRecentLogs()
        {
            lock (_lock)
            {
                return _logs.ToList().AsReadOnly();
            }
        }
    }
}
