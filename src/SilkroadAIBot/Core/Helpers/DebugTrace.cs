using System;
using System.IO;
using System.Text;
using System.Threading;

namespace SilkroadAIBot.Core.Helpers
{
    /// <summary>
    /// v1.3.3 — Dedicated debug trace file writer.
    /// Writes verbose per-packet and per-function traces to a separate
    /// debug_YYYYMMDD.log file (never shown in UI, never written to BotLogger).
    /// Thread-safe via a dedicated queue and background flush loop.
    /// </summary>
    public static class DebugTrace
    {
        private static string _logPath = "";
        private static StreamWriter? _writer;
        private static readonly object _lock = new object();
        private static bool _enabled = false;

        public static void Enable(string logsDir)
        {
            if (_enabled) return;
            try
            {
                Directory.CreateDirectory(logsDir);
                string filename = $"debug_{DateTime.Now:yyyyMMdd}.log";
                _logPath = Path.Combine(logsDir, filename);
                _writer = new StreamWriter(_logPath, append: true, Encoding.UTF8)
                {
                    AutoFlush = false
                };
                _enabled = true;
                Raw($"[DebugTrace] === Session Start [{DateTime.Now:yyyy-MM-dd HH:mm:ss}] ===");

                // Background flush every 500ms
                var t = new Thread(() =>
                {
                    while (_enabled)
                    {
                        Thread.Sleep(500);
                        Flush();
                    }
                }) { IsBackground = true, Name = "DebugTrace.Flush" };
                t.Start();
            }
            catch { }
        }

        public static void Disable()
        {
            _enabled = false;
            Flush();
            _writer?.Close();
        }

        public static void Packet(string direction, ushort opcode, int size, string detail = "")
        {
            if (!_enabled) return;
            Raw($"[PKT] {direction} 0x{opcode:X4} ({size}B){(string.IsNullOrEmpty(detail) ? "" : " | " + detail)}");
        }

        public static void PacketField(string packetName, string fieldName, object value)
        {
            if (!_enabled) return;
            Raw($"[FIELD] {packetName}.{fieldName} = {value}");
        }

        public static void PacketError(string packetName, string error, byte[]? raw = null)
        {
            if (!_enabled) return;
            string hex = raw != null ? BitConverter.ToString(raw).Replace("-", " ") : "";
            Raw($"[PKT-ERR] {packetName}: {error}{(raw != null ? " | raw=" + hex : "")}");
        }

        public static void BotDecision(string bundle, string action, string reason)
        {
            if (!_enabled) return;
            Raw($"[BOT] [{bundle}] {action} | {reason}");
        }

        public static void WorldState(string context, string detail)
        {
            if (!_enabled) return;
            Raw($"[WS] [{context}] {detail}");
        }

        public static void Inventory(string context, string detail)
        {
            if (!_enabled) return;
            Raw($"[INV] [{context}] {detail}");
        }

        public static void Raw(string message)
        {
            if (!_enabled) return;
            lock (_lock)
            {
                try
                {
                    _writer?.WriteLine($"[{DateTime.Now:HH:mm:ss.fff}] {message}");
                }
                catch { }
            }
        }

        private static void Flush()
        {
            lock (_lock)
            {
                try { _writer?.Flush(); } catch { }
            }
        }
    }
}
