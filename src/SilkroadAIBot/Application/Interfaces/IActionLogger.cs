using System;
using System.Collections.Generic;

namespace SilkroadAIBot.Application.Interfaces
{
    /// <summary>
    /// Service for logging bot actions and events.
    /// Acts as the source of truth for ActionLogs in IWorldStateRepository.
    /// </summary>
    public interface IActionLogger
    {
        /// <summary>
        /// Logs a new action message.
        /// </summary>
        /// <param name="message">The message to log.</param>
        void Log(string message);

        /// <summary>
        /// Returns a snapshot of the most recent logs.
        /// </summary>
        IReadOnlyList<string> GetRecentLogs();

        /// <summary>
        /// Event fired when a new log entry is added.
        /// </summary>
        event Action<string> OnLogAdded;
    }
}
