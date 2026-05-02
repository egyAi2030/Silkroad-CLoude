using System;
using System.Text;

namespace SilkroadAIBot.Core.Helpers
{
    public static class GameDataHelpers
    {
        /// <summary>
        /// Converts a DDJ icon path to a standard DDS path.
        /// </summary>
        public static string GetStandardIconPath(string pk2Path)
        {
            if (string.IsNullOrEmpty(pk2Path)) return "";
            return pk2Path.Replace(".ddj", ".dds");
        }

        /// <summary>
        /// Strips the 20-byte JMXVDDJ header from DDJ data to get raw DDS data.
        /// </summary>
        public static byte[] StripDDJHeader(byte[] ddjData)
        {
            if (ddjData == null || ddjData.Length <= 20) return ddjData!;
            
            // Check for JMXVDDJ 1000
            try
            {
                string header = Encoding.ASCII.GetString(ddjData, 0, 12);
                if (header == "JMXVDDJ 1000")
                {
                    byte[] ddsData = new byte[ddjData.Length - 20];
                    Array.Copy(ddjData, 20, ddsData, 0, ddsData.Length);
                    return ddsData;
                }
            }
            catch { }
            
            return ddjData;
        }
    }
}
