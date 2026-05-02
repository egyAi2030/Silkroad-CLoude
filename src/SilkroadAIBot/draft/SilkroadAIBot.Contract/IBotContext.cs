using System;

namespace SilkroadAIBot.Contract
{
    public interface IBotContext
    {
        void Log(string message);
        void SendPacket(byte[] buffer); // Simplified for plugin usage
        // Add more API methods as needed
    }
}
