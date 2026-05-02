using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Domain.Network;
using SilkroadAIBot.Infrastructure.Networking;
using SilkroadAIBot.Data;
using SilkroadAIBot.Application.Interfaces;

namespace SilkroadAIBot.Networking
{
    public class LoginManager
    {
        private ClientlessConnection _connection;
        public ClientlessConnection Connection => _connection;
        
        private bool _isLoginSuccess;
        private string _username = null!;
        private string _password = null!;
        
        private IWorldStateRepository _worldState;
        private readonly CharacterDataBuffer _charBuffer = new CharacterDataBuffer();
        private readonly WorldStateAnalyzer _analyzer;

        public LoginManager(IWorldStateRepository worldState, IEntityRepository entityRepo, DataManager dataManager)
        {
            _worldState = worldState;
            _analyzer   = new WorldStateAnalyzer(entityRepo, worldState, dataManager);
            _connection = new ClientlessConnection();
            _connection.OnPacketReceived += HandlePacket;
        }

        public async Task<bool> LoginAsync(string gatewayIp, int port, string username, string password)
        {
            _username = username;
            _password = password;
            _isLoginSuccess = false;

            BotLogger.Info($"[Login] Connecting to Gateway {gatewayIp}:{port}...");
            try 
            {
                await _connection.ConnectAsync(gatewayIp, port);
            }
            catch (Exception ex)
            {
                BotLogger.Error($"[Login] Could not connect to Gateway: {ex.Message}");
                return false;
            }

            // Startup packet processing
            _ = Task.Run(() => _connection.ProcessAsync());

            // Wait for full login cycle (Gateway -> Redirect -> Agent Ready)
            int timeout = 30000; 
            int delay = 100;
            while (timeout > 0)
            {
                if (_isLoginSuccess) return true;
                await Task.Delay(delay);
                timeout -= delay;
            }

            BotLogger.Warn("[Login] Timeout waiting for authentication/redirection.");
            return false;
        }

        private void HandlePacket(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            switch (packet.Opcode)
            {
                case Opcode.GLOBAL_IDENTIFICATION: // 0x2001
                    string service = reader.ReadAscii();
                    if (service == "GatewayServer")
                    {
                        BotLogger.Info("[Login] Gateway identified. Handshake Step 1: Identity & Patch Check...");
                        
                        using var ident = new SRPacketWriter(Opcode.GLOBAL_IDENTIFICATION);
                        ident.WriteAscii("SR_Client");
                        _connection.SendPacket(ident.Build());

                        SendPatchCheck();
                    }
                    else if (service == "AgentServer")
                    {
                        BotLogger.Info("[Agent] Connected to Agent. Handshake Step 1: Identity...");
                        using var ident = new SRPacketWriter(Opcode.GLOBAL_IDENTIFICATION);
                        ident.WriteAscii("SR_Client");
                        _connection.SendPacket(ident.Build());
                    }
                    break;

                case Opcode.SERVER_PATCH_RESPONSE: // 0xA100
                    BotLogger.Info("[Login] Patch OK. Step 2: Requesting Server List...");
                    SendServerListRequest();
                    break;

                case Opcode.SERVER_SERVER_LIST_RESPONSE: // 0xA101
                    BotLogger.Info("[Login] Server List received. Waiting 9 seconds before sending credentials...");
                    _ = Task.Run(async () => {
                        await Task.Delay(9000);
                        SendLoginRequest();
                    });
                    break;

                case Opcode.SERVER_CAPTCHA_CHALLENGE: // 0x2322
                    BotLogger.Info("[Login] Captcha Challenge received. Sending Empty Response...");
                    SendCaptchaResponse();
                    break;

                case Opcode.SERVER_LOGIN_REDIRECT: // 0xA102
                    ParseLoginRedirect(packet);
                    break;

                case Opcode.SERVER_LOGIN_ERROR: // 0xA103
                    byte errorCode = reader.ReadByte();
                    BotLogger.Warn($"[Login] Authentication Failed. Error Code: {errorCode:X2}");
                    if (errorCode == 0x07) BotLogger.Error("[Login] REASON: Invalid username or password.");
                    break;

                case Opcode.SERVER_CHARACTER_SELECTION_ACTION_RESPONSE: // 0xB007 (Char List)
                    BotLogger.Info("[Agent] Character List Received. Consolidating with Analyzer...");
                    _analyzer.ParseCharacterSelectionAction(packet);
                    
                    if (!string.IsNullOrEmpty(_worldState.CharacterName))
                    {
                        BotLogger.Info($"[Agent] Analyzer identified character: {_worldState.CharacterName}. Selecting...");
                        SelectCharacter(_worldState.CharacterName);
                    }
                    else
                    {
                        BotLogger.Warn("[Agent] No character identified by analyzer for selection.");
                    }
                    break;

                case Opcode.SERVER_CHARACTER_DATA_BEGIN: // 0x34A5
                    BotLogger.Info("[Agent] Receiving Character Data (Chunked)...");
                    _charBuffer.Reset();
                    break;

                case Opcode.SERVER_CHARACTER_DATA:      // 0x3013 chunk
                    if (_charBuffer.IsBuffering)
                        _charBuffer.Append(packet.Payload); // packet.Payload IS the chunk
                    break;

                case Opcode.SERVER_CHARACTER_DATA_END: // 0x34A6
                    BotLogger.Info("[Agent] Character Data Complete. Finalizing Assembly...");
                    var raw = _charBuffer.FinalizeAndGet();
                    var assembled = new SRPacket(Opcode.SERVER_CHARACTER_DATA, raw, false);
                    _analyzer.ParseCharacterData(assembled);
                    BotLogger.Info("[Agent] Confirming Spawn (0x3012)...");
                    ConfirmSpawn();
                    _isLoginSuccess = true;
                    break;
            }
        }

        private void SendPatchCheck()
        {
            using var writer = new SRPacketWriter(Opcode.CLIENT_PATCH_CHECK, true);
            writer.WriteByte(22); // Locale
            writer.WriteAscii("Gateway");
            writer.WriteUInt32(188); // Version
            _connection.SendPacket(writer.Build());
        }

        private void SendServerListRequest()
        {
            using var writer = new SRPacketWriter(Opcode.CLIENT_SERVER_LIST_REQUEST, true);
            _connection.SendPacket(writer.Build());
        }

        private void SendLoginRequest()
        {
            using var writer = new SRPacketWriter(Opcode.CLIENT_LOGIN_REQUEST, true);
            writer.WriteByte(22); // Locale (VSRO)
            writer.WriteAscii(_username);
            writer.WriteAscii(_password);
            writer.WriteUInt16(64); // Server ID
            _connection.SendPacket(writer.Build());
        }

        private void SendCaptchaResponse()
        {
            using var writer = new SRPacketWriter(Opcode.CLIENT_CAPTCHA_RESPONSE, true);
            writer.WriteAscii(""); 
            _connection.SendPacket(writer.Build());
        }

        private async void ParseLoginRedirect(SRPacket packet)
        {
            using var reader = new SRPacketReader(packet);
            byte result = reader.ReadByte();
            if (result == 1)
            {
                uint sessionID = reader.ReadUInt32();
                string agentIP = reader.ReadAscii();
                ushort agentPort = reader.ReadUInt16();

                BotLogger.Info($"[Login] Authentication Success! Redirecting to Agent: {agentIP}:{agentPort} (SessionID: {sessionID})");
                
                _connection.Dispose();
                _connection = new ClientlessConnection();
                _connection.OnPacketReceived += HandlePacket; // Re-use this handler for agent flow
                
                BotLogger.Info("[Login] Connecting to Agent Server...");
                try 
                {
                    await _connection.ConnectAsync(agentIP, agentPort);
                    _ = Task.Run(() => _connection.ProcessAsync());
                    
                    await Task.Delay(500);
                    SendAgentAuth(sessionID);
                    BotLogger.Info("[Login] Agent Authentication (0x6103) sent. Waiting for character list...");
                }
                catch (Exception ex)
                {
                    BotLogger.Error($"[Login] Failed to connect to Agent Server: {ex.Message}");
                }
            }
            else
            {
                BotLogger.Warn($"[Login] Authentication Redirect Failed. Result: {result}");
            }
        }

        private void SendAgentAuth(uint sessionID)
        {
            using var writer = new SRPacketWriter(Opcode.CLIENT_AGENT_AUTH, true);
            writer.WriteUInt32(sessionID);
            writer.WriteAscii(_username);
            writer.WriteAscii(_password);
            _connection.SendPacket(writer.Build());
        }

        public void SelectCharacter(string charName)
        {
             BotLogger.Info($"[Agent] Selecting Character: {charName}");
             using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_SELECTION_JOIN_REQUEST, true);
             writer.WriteAscii(charName);
             _connection.SendPacket(writer.Build());
        }
        
        public void ConfirmSpawn()
        {
            using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_CONFIRM_SPAWN);
            _connection.SendPacket(writer.Build());
            BotLogger.Info("[Agent] Character entered game world successfully.");
        }
        
        public void Disconnect()
        {
            _connection.Dispose();
        }
    }
}

