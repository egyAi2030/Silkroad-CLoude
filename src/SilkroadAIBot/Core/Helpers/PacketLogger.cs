using System;
using System.IO;
using System.Text;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Infrastructure.Networking;

namespace SilkroadAIBot.Core.Helpers
{
    public class PacketLogger
    {
        private static string _logPath = string.Empty;
        private static bool _isEnabled = true;
        private static readonly object _lock = new object();

        static PacketLogger()
        {
            string logDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
            if (!Directory.Exists(logDir)) Directory.CreateDirectory(logDir);

            _logPath = Path.Combine(logDir, $"packets_{DateTime.Now:yyyyMMdd}.log");
            
            // Subscribe to global packet event
            PacketDispatcher.OnGlobalPacketReceived += (pkt, isSent) => LogPacket(pkt, isSent);
        }

        public static void LogPacket(SilkroadAIBot.Domain.Network.SRPacket packet, bool isSent)
        {
            if (!_isEnabled) return;

            try
            {
                lock (_lock)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine("--------------------------------------------------------------------------------");
                    sb.AppendLine($"[{DateTime.Now:HH:mm:ss.fff}] {(isSent ? "[CLIENT -> SERVER]" : "[SERVER -> CLIENT]")}");
                    sb.AppendLine($"Opcode: 0x{packet.Opcode:X4} | Size: {packet.Data.Length} bytes");
                    
                    byte[] data = packet.Data;
                    if (data != null && data.Length > 0)
                    {
                        sb.AppendLine(HexDump(data));
                    }
                    else
                    {
                        sb.AppendLine("(No Data)");
                    }
                    
                    File.AppendAllText(_logPath, sb.ToString());

                    // Handshake highlights in main bot log
                    if (packet.Opcode == 0x5000 || packet.Opcode == 0x9000 || packet.Opcode == 0xA103 || packet.Opcode == 0x2001)
                    {
                         BotLogger.Info("Handshake", $"Captured 0x{packet.Opcode:X4} ({(isSent ? "C>S" : "S<C")}) - Data logged to packets_{DateTime.Now:yyyyMMdd}.log");
                    }
                }
            }
            catch { }
        }

        private static string HexDump(byte[] bytes, int bytesPerLine = 16)
        {
            if (bytes == null) return "<null>";
            int len = bytes.Length;
            StringBuilder sb = new StringBuilder();

            for (int i = 0; i < len; i += bytesPerLine)
            {
                // Offset
                sb.Append($"{i:X4}: ");

                // Hex
                int j;
                for (j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < len)
                        sb.Append($"{bytes[i + j]:X2} ");
                    else
                        sb.Append("   ");
                }

                sb.Append(" | ");

                // ASCII
                for (j = 0; j < bytesPerLine; j++)
                {
                    if (i + j < len)
                    {
                        char c = (char)bytes[i + j];
                        sb.Append(char.IsControl(c) ? '.' : c);
                    }
                }
                sb.AppendLine();
            }
            return sb.ToString();
        }

        public static void SetEnabled(bool enabled) => _isEnabled = enabled;
    }
}
