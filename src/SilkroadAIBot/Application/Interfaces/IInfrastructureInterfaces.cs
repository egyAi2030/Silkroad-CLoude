using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Application.Interfaces
{
    /// <summary>
    /// Shared buffer for the 3-opcode character data assembly sequence:
    /// 0x34A5 (begin) → 0x3013 × N (chunks) → 0x34A6 (end).
    /// Injected into all three handler classes so they share the same state.
    /// Thread-safety is the caller's responsibility — all three handlers
    /// run on the same <see cref="IPacketDispatcher"/> consumer thread.
    /// </summary>
    public interface ICharacterDataBuffer
    {
        /// <summary>Clears the buffer and sets buffering state to active. Called on 0x34A5.</summary>
        void Reset();

        /// <summary>Appends raw bytes to the buffer. Called on each 0x3013 chunk.</summary>
        /// <param name="data">Raw bytes read from the current chunk packet.</param>
        void Append(byte[] data);

        /// <summary>Returns all accumulated bytes and resets state. Called on 0x34A6.</summary>
        byte[] FinalizeAndGet();

        /// <summary>Returns <c>true</c> if a buffering sequence is currently active.</summary>
        bool IsBuffering { get; }

        /// <summary>Total number of bytes accumulated in the current sequence.</summary>
        int ByteCount { get; }
    }

    /// <summary>
    /// Provides navigation mesh data for pathfinding (A* — Module 8).
    /// Implementations use an LRU cache with a maximum of 16 loaded regions
    /// to bound memory usage.
    /// </summary>
    public interface INavMeshProvider
    {
        /// <summary>
        /// Returns whether the straight-line path from <paramref name="from"/>
        /// to <paramref name="to"/> is unobstructed by terrain geometry.
        /// </summary>
        /// <param name="from">Start coordinate.</param>
        /// <param name="to">End coordinate.</param>
        bool HasLineOfSight(SRCoord from, SRCoord to);

        /// <summary>
        /// Returns whether <paramref name="coord"/> is a walkable cell.
        /// </summary>
        bool IsWalkable(SRCoord coord);

        /// <summary>
        /// Returns the neighbours of the nav cell containing <paramref name="coord"/>.
        /// Used by the A* open-set expansion step.
        /// </summary>
        /// <param name="coord">The coordinate whose nav cell neighbours are requested.</param>
        IReadOnlyList<SRCoord> GetNeighbours(SRCoord coord);

        /// <summary>
        /// Ensures the nav mesh region containing <paramref name="coord"/> is loaded.
        /// Evicts the least-recently-used region if the cache is full (max 16).
        /// </summary>
        /// <param name="coord">Coordinate whose region must be available.</param>
        /// <param name="ct">Cancellation token.</param>
        Task EnsureLoadedAsync(SRCoord coord, CancellationToken ct);
    }

    /// <summary>
    /// Extracts game data from the PK2 archive and persists it to the SQLite database.
    /// Called once on first run; subsequent runs load from the database cache.
    /// </summary>
    public interface IDataExtractor
    {
        /// <summary>
        /// Returns <c>true</c> if the database already contains extracted data
        /// and a full re-extraction is not needed.
        /// </summary>
        bool IsCacheValid { get; }

        /// <summary>
        /// Runs the full extraction pipeline: reads PK2 files, parses textdata,
        /// and writes to SQLite in one transaction.
        /// </summary>
        /// <param name="pk2Path">Absolute path to the Media.pk2 file.</param>
        /// <param name="progress">Optional progress reporter (0–100).</param>
        /// <param name="ct">Cancellation token.</param>
        Task ExtractAsync(string pk2Path, System.IProgress<int>? progress, CancellationToken ct);
    }

    /// <summary>
    /// Provides the set of MCP tools exposed to the LLM agent.
    /// Implementations return one <see cref="McpTool"/> descriptor per tool,
    /// each containing the tool name, description, JSON schema, and handler delegate.
    /// </summary>
    public interface IMcpToolProvider
    {
        /// <summary>Returns all tools this provider exposes to the MCP server.</summary>
        IReadOnlyList<McpTool> GetTools();

        /// <summary>Invokes a tool by name with the given arguments.</summary>
        Task<object> CallToolAsync(string name, Dictionary<string, object> arguments);
    }

    /// <summary>
    /// Descriptor for a single MCP tool callable by the LLM agent.
    /// </summary>
    /// <param name="Name">Tool name (snake_case, e.g. <c>get_world_state</c>).</param>
    /// <param name="Description">Human-readable description shown to the LLM.</param>
    /// <param name="InputSchema">JSON Schema string describing the tool's input parameters.</param>
    /// <param name="Handler">
    /// Async delegate invoked when the agent calls this tool.
    /// Receives the raw JSON arguments string; returns the result as a JSON string.
    /// </param>
    public record McpTool(
        string Name,
        string Description,
        string InputSchema,
        System.Func<string, CancellationToken, Task<string>> Handler);

    /// <summary>
    /// Provides read access to the bot's session log for the MCP <c>get_session_log</c> tool.
    /// The underlying buffer is a ring buffer with a fixed capacity (5000 lines).
    /// </summary>
    public interface ISessionLogger
    {
        /// <summary>
        /// Returns the last <paramref name="count"/> log lines, newest last.
        /// If <paramref name="count"/> exceeds the number of available lines,
        /// all available lines are returned.
        /// </summary>
        /// <param name="count">Maximum number of lines to return.</param>
        IReadOnlyList<string> GetRecentLines(int count);

        /// <summary>
        /// Appends a formatted log line to the session buffer.
        /// Format: <c>[HH:mm:ss] [LEVEL] [SOURCE] message</c>
        /// </summary>
        /// <param name="level">Log level label (e.g., "INFO", "WARN", "ERROR").</param>
        /// <param name="source">Source component name (e.g., "ProxyContext").</param>
        /// <param name="message">The log message body.</param>
        void Append(string level, string source, string message);

        /// <summary>Total number of lines currently in the buffer.</summary>
        int Count { get; }
    }
}
