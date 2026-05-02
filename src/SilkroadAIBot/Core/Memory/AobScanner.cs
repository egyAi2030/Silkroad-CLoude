using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Core.Memory
{
    public class AobScanner
    {
        private System.Diagnostics.Process _process;
        private ProcessMemory _memory;

        public AobScanner(System.Diagnostics.Process process, ProcessMemory memory)
        {
            _process = process;
            _memory = memory;
        }

        public IntPtr FindSignature(string signature)
        {
            byte?[] pattern = ParseSignature(signature);
            if (pattern == null || pattern.Length == 0) return IntPtr.Zero;

            var regions = _memory.GetReadableRegions();
            foreach (var region in regions)
            {
                byte[] buffer = _memory.Read(region.BaseAddress, (int)region.RegionSize.ToInt64());
                if (buffer == null) continue;

                for (int i = 0; i < buffer.Length - pattern.Length; i++)
                {
                    if (MatchPattern(buffer, i, pattern))
                    {
                        return IntPtr.Add(region.BaseAddress, i);
                    }
                }
            }

            return IntPtr.Zero;
        }

        public IntPtr FindString(string text, bool unicode = false)
        {
            byte[] patternBytes = unicode ? System.Text.Encoding.Unicode.GetBytes(text) : System.Text.Encoding.ASCII.GetBytes(text);
            byte?[] pattern = new byte?[patternBytes.Length];
            for (int i = 0; i < patternBytes.Length; i++) pattern[i] = patternBytes[i];

            var regions = _memory.GetReadableRegions();
            foreach (var region in regions)
            {
                byte[] buffer = _memory.Read(region.BaseAddress, (int)region.RegionSize.ToInt64());
                if (buffer == null) continue;

                for (int i = 0; i < buffer.Length - pattern.Length; i++)
                {
                    if (MatchPattern(buffer, i, pattern))
                    {
                        return IntPtr.Add(region.BaseAddress, i);
                    }
                }
            }

            return IntPtr.Zero;
        }

        private bool MatchPattern(byte[] data, int index, byte?[] pattern)
        {
            for (int i = 0; i < pattern.Length; i++)
            {
                if (pattern[i].HasValue && data[index + i] != pattern[i].Value)
                    return false;
            }
            return true;
        }

        private byte?[] ParseSignature(string signature)
        {
            string[] parts = signature.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            byte?[] pattern = new byte?[parts.Length];

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "??" || parts[i] == "?")
                {
                    pattern[i] = null;
                }
                else
                {
                    pattern[i] = Convert.ToByte(parts[i], 16);
                }
            }

            return pattern;
        }

        public void DumpMemoryStrings(string filePath)
        {
            try
            {
                var regions = _memory.GetReadableRegions();
                using (var sw = new System.IO.StreamWriter(filePath))
                {
                    sw.WriteLine($"--- Memory String Dump for PID {_process.Id} ---");
                    foreach (var region in regions)
                    {
                        byte[] buffer = _memory.Read(region.BaseAddress, (int)region.RegionSize.ToInt64());
                        if (buffer == null) continue;

                        string ascii = System.Text.Encoding.ASCII.GetString(buffer);
                        var matches = System.Text.RegularExpressions.Regex.Matches(ascii, @"[a-zA-Z0-9\.\-_]{5,}");
                        foreach (System.Text.RegularExpressions.Match match in matches)
                        {
                            if (match.Value.Contains(".") && match.Value.Length > 8)
                            {
                                sw.WriteLine($"[ASCII] 0x{IntPtr.Add(region.BaseAddress, match.Index).ToInt64():X8}: {match.Value}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Error("Scanner", $"Failed to dump memory strings: {ex.Message}");
            }
        }
    }
}
