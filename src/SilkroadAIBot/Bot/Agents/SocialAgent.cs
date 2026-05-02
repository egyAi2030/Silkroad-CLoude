using System;
using System.Threading.Tasks;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Core.Settings;

namespace SilkroadAIBot.Bot.Agents
{
    public enum AIPersona
    {
        FRIENDLY,
        AGGRESSIVE,
        SILENT,
        DECEPTIVE
    }

    /// <summary>
    /// v1.5.0 — Handles social interactions and intelligent chat processing.
    /// Integrates with LLM providers for human-like responses.
    /// </summary>
    public class SocialAgent
    {
        private readonly WorldState _worldState;
        private readonly PacketSender _sender;
        
        public AIPersona CurrentPersona { get; set; } = AIPersona.FRIENDLY;
        public bool IsAIResponseEnabled { get; set; } = false;

        public SocialAgent(WorldState worldState, PacketSender sender)
        {
            _worldState = worldState;
            _sender = sender;
        }

        /// <summary>
        /// Analyzes incoming chat and generates a response using the selected LLM context.
        /// </summary>
        public async Task ProcessIncomingChatAsync(string senderName, byte type, string message)
        {
            if (!IsAIResponseEnabled) return;

            BotLogger.Info("SocialAI", $"Analyzing [{GetChatTypeName(type)}] from {senderName}: \"{message}\"");

            // 1. Context check (Is it a GM? Is it a trade request? Is it someone asking if I'm a bot?)
            if (message.ToLower().Contains("bot") || message.ToLower().Contains("gm") || message.ToLower().Contains("admin"))
            {
                BotLogger.Warn("SocialAI", "Critical keyword detected! Escalating to High Priority AI Response.");
                await GenerateAIResponseAsync(senderName, type, message, highPriority: true);
            }
            else if (CurrentPersona != AIPersona.SILENT)
            {
                await GenerateAIResponseAsync(senderName, type, message, highPriority: false);
            }
        }

        private async Task GenerateAIResponseAsync(string target, byte type, string input, bool highPriority)
        {
            // PROMPT CONSTRUCTION
            string personaPrompt = GetPersonaPrompt();
            string fullPrompt = $"Persona: {personaPrompt}\nChannel: {GetChatTypeName(type)}\nFrom: {target}\nInput: {input}\nResponse (Short & Natural):";

            BotLogger.Debug("SocialAI", $"Generating AI completion for {target}...");

            try 
            {
                // MOCK LLM CALL (Will be replaced by actual HttpClient call to OpenAI/Gemini in final v1.5.1)
                string response = "I'm just busy hunting right now, talk later!"; // Placeholder
                
                if (highPriority) response = "Haha, no I'm just watching Netflix while grinding, why?";

                // Add random delay to simulate human typing
                await Task.Delay(new Random().Next(2000, 5000));

                SendChatMessage(type, target, response);
            }
            catch (Exception ex)
            {
                BotLogger.Error("SocialAI", $"Failed to generate AI response: {ex.Message}");
            }
        }

        private string GetPersonaPrompt()
        {
            return CurrentPersona switch
            {
                AIPersona.FRIENDLY => "You are a helpful and kind Silkroad player. Use emojis sparingly.",
                AIPersona.AGGRESSIVE => "You are an elitist player, annoyed by interruptions. Be brief and cold.",
                AIPersona.DECEPTIVE => "You are trying to hide that you are a bot. Act casual, use slang like 'lol', 'asap', 'brb'.",
                _ => "Be a normal player."
            };
        }

        public void SendChatMessage(byte type, string receiver, string message)
        {
            BotLogger.Info("SocialAI", $"Auto-Response to {receiver}: {message}");
            
            // 0x7025 is standard chat packet
            using var writer = new Domain.Network.SRPacketWriter(0x7025);
            writer.WriteByte(type);
            writer.WriteByte(0); // Index for Union/Guild (usually 0)
            
            if (type == 2) // Whisper
                writer.WriteAscii(receiver);
                
            writer.WriteAscii(message);
            _sender.SendPacket(writer.Build());
        }

        private string GetChatTypeName(byte type)
        {
            return type switch { 1 => "All", 2 => "Whisper", 3 => "Party", 4 => "Guild", 5 => "Global", _ => "Unknown" };
        }
    }
}

