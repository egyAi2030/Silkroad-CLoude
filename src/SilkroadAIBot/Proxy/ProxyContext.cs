using System;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Bot;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Core.Configuration;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Domain.Network;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Infrastructure.Networking;
using SilkroadAIBot.Infrastructure.Security;
using SilkroadAIBot.Data;

namespace SilkroadAIBot.Proxy
{
    public class ProxyContext
    {
        private TcpClient _gameClient;
        private ClientlessConnection _serverConnection;
        private bool _leg1Initialized = false; // Bot -> Server
        private int _targetPort;
        private bool _isClosing = false;
        public bool IsActive => !_isClosing && _gameClient.Connected;
        
        private byte[]? _capturedF000Payload = null;
        private uint _agentToken = 0;
        
        private ISecurityService _clientSecurity;
        private WorldState _worldState;
        private PacketDispatcher? _dispatcher;
        private CancellationTokenSource? _dispatcherCts;
        
        public ProxyContext(TcpClient gameClient, WorldState worldState)
        {
            _gameClient = gameClient;
            _worldState = worldState;
            _clientSecurity = new SecurityService();
            
            BotLogger.Info("Proxy", "Leg 1: Initializing Bot-as-Server Security Context...");
        }
        
        public void Initialize(DataManager dataManager)
        {
            var factory  = new PacketHandlerFactory();
            var buffer   = new CharacterDataBuffer();
            var analyzer = new WorldStateAnalyzer(_worldState, _worldState, dataManager);
            var parser   = new PacketParser(_worldState, _worldState, dataManager);
            
            var sender = new PacketSender(_worldState, () => _serverConnection);
            HandlerRegistration.RegisterAll(factory, buffer, analyzer, parser, _worldState, dataManager, sender);
            _dispatcherCts = new CancellationTokenSource();
            _dispatcher = new PacketDispatcher(factory);
            _ = _dispatcher.StartAsync(_dispatcherCts.Token);
            BotLogger.Info("Proxy", "PacketDispatcher started — all handlers registered.");
        }

        public ClientlessConnection? ServerConnection => _serverConnection;

        public async Task StartProxyAsync(string targetIP, int targetPort)
        {
             _targetPort = targetPort;
             BotLogger.Info("Proxy", $"Connecting to Remote Server {targetIP}:{targetPort}...");
             
             try
             {
                 _serverConnection = new ClientlessConnection();
                 await _serverConnection.ConnectAsync(targetIP, targetPort);
                 
                 // v1.1.26: Start keepalive loop to prevent server timeout (0x2002 every 5s)
                 StartKeepaliveLoop();
                 
                 BotLogger.Info("Proxy", "Server connection established. Starting relay logic...");

                 _ = Task.Run(() => RelayLoop(_gameClient.GetStream(), _serverConnection));
                 
                 BotLogger.Info("Proxy", "Session active. Waiting for Handshake...");
             }
             catch (Exception ex)
             {
                 BotLogger.Error("Proxy", $"Failed to establish server connection: {ex.Message}");
                 throw;
             }
        }
        
        private async Task RelayLoop(NetworkStream clientStream, ClientlessConnection server)
        {
            var clientToServer = RelayClientToServer(clientStream, server);
            var serverToClient = RelayServerToClient(server, clientStream);
            
            await Task.WhenAny(clientToServer, serverToClient);
            BotLogger.Info("Proxy", "[Proxy] Session Ended.");
        }

        private async Task RelayClientToServer(NetworkStream clientStream, ClientlessConnection server)
        {
            try 
            {
                byte[] buffer = new byte[8192];

                while (_gameClient.Connected)
                {
                    int bytesRead = await clientStream.ReadAsync(buffer, 0, buffer.Length);
                    if (bytesRead == 0) break;
                    
                    _clientSecurity.Recv(buffer, 0, bytesRead);
                    var packets = _clientSecurity.GetIncomingPackets();
                    
                    foreach(var packet in packets)
                    {
                        if (packet.Opcode == 0x5000)
                        {
                            continue;
                        }
                        
                        if (packet.Opcode == Opcode.CLIENT_VPLUS_AUTH_TOKEN)
                        {
                            BotLogger.Info("Proxy", $"vSRO Plus Security Token (0xF000) detected. Relaying...");
                            if (_targetPort == ProxyManager.Instance.TargetGatewayPort)
                            {
                                _capturedF000Payload = packet.Payload;
                                ProxyManager.Instance.CapturedF000Payload = _capturedF000Payload;
                            }
                        }

                        // v1.1.20: Enhanced Locale Patching (RexallSRO requires 0x16)
                        if (packet.Opcode == Opcode.CLIENT_PATCH_CHECK || packet.Opcode == Opcode.CLIENT_LOGIN_REQUEST)
                        {
                            if (packet.Payload.Length > 0 && packet.Payload[0] == 0x00)
                            {
                                BotLogger.Warn("Proxy", $"Hot Patch: 0x{packet.Opcode:X4} locale 00 -> 16.");
                                packet.Payload[0] = 0x16; // Hot patch directly in payload
                            }
                            
                            if (packet.Opcode == Opcode.CLIENT_LOGIN_REQUEST && _targetPort != ProxyManager.Instance.TargetGatewayPort)
                            {
                                BotLogger.Error("Proxy", "[SECURITY] Blocking 0x6102 on Agent leg.");
                                continue;
                            }
                        }

                        if (packet.Opcode == Opcode.CLIENT_AGENT_AUTH)
                        {
                            try
                            {
                                using var reader = new SRPacketReader(packet);
                                reader.ReadUInt32(); // Token
                                reader.ReadAscii();  // Username
                                reader.ReadAscii();  // Password
                                int localePos = (int)reader.Position;

                                if (packet.Payload.Length > localePos)
                                {
                                    BotLogger.Info("Proxy", $"[Agent] Dynamic Patch: 0x6103 locale at index {localePos} -> 0x16");
                                    packet.Payload[localePos] = 0x16;
                                }
                            }
                            catch (Exception ex)
                            {
                                BotLogger.Error("Proxy", $"[Agent] Failed to parse 0x6103 for dynamic patching: {ex.Message}");
                            }
                        }

                        if (packet.Opcode == Opcode.CLIENT_CHARACTER_SELECTION_JOIN_REQUEST)
                        {
                            using var reader = new SRPacketReader(packet);
                            string selectedName = reader.ReadAscii();
                             _worldState.CharacterName = selectedName;
                             _worldState.Character = _worldState.Character with { Name = selectedName };
                            BotLogger.Info("Proxy", $"[Proxy] Intercepted Client Character Selection: '{selectedName}'.");
                        }

                        if (packet.Opcode == Opcode.CLIENT_CHARACTER_ACTION_REQUEST_SELECTION)
                        {
                            try
                            {
                                using var reader = new SRPacketReader(packet);
                                uint uid = reader.ReadUInt32();
                                if (reader.Remaining >= 2)
                                {
                                    string hash = reader.ReadAscii();
                                    _worldState.LastSelectionHash = hash;
                                    BotLogger.Debug("Proxy", $"[Sync] Captured Selection Hash: {hash}");
                                }
                            }
                            catch { }
                        }

                        PacketDispatcher.TriggerGlobalPacket(packet, true);
                        server.SendPacket(packet);
                    }
                    
                    foreach (var outBytes in _clientSecurity.GetOutgoingBytes())
                    {
                        await clientStream.WriteAsync(outBytes, 0, outBytes.Length);
                    }
                }
            }
            catch (Exception ex) 
            { 
                if (!_isClosing) BotLogger.Error("Proxy", $"C->S Error: {ex.Message}"); 
            }
        }

        private async Task RelayServerToClient(ClientlessConnection server, NetworkStream clientStream)
        {
             try 
            {
                // server.HandshakePassive removed — logic is handled via ProcessesAsync/PerformHandshakeAsync
                _ = server.ProcessAsync(); 
                
                while (_gameClient.Connected && server.IsConnected)
                {
                    var packet = server.GetNextPacket();
                    
                    if (packet != null)
                    {
                        if (packet.Opcode == 0x5000)
                        {
                            if (!_leg1Initialized)
                            {
                                BotLogger.Info("Proxy", $"Leg 2 Handshake: Server -> Bot 0x5000 (Initial)");
                                _leg1Initialized = true;
                                uint trueCount = server.Security.CountSeed;
                                uint trueCRC = server.Security.CRCSeed;
                                ulong trueBf = server.Security.InitialBlowfishKey;
                                
                                BotLogger.Info("Proxy", $"Leg 1 Sync: Stolen Seeds [Count: {trueCount:X8}, CRC: {trueCRC:X8}, BF: {trueBf:X16}]");
                                _clientSecurity.InitializeAsServer(trueCount, trueCRC, trueBf);

                                foreach (var outBytes in _clientSecurity.GetOutgoingBytes())
                                {
                                     await clientStream.WriteAsync(outBytes, 0, outBytes.Length);
                                }
                                continue;
                            }
                            else
                            {
                                BotLogger.Info("Proxy", "Leg 2 Handshake: Server -> Bot 0x5000 (Security Bytes) - Relaying to Client");
                                // Fall through to FormatPacket and relay
                            }
                        }

                        if (packet.Opcode == 0x9000)
                        {
                            BotLogger.Info("Proxy", "Leg 2 Handshake: Server -> Bot 0x9000 - Relaying to Client");
                            // Fall through to FormatPacket and relay
                        }

                        if (packet.Opcode == Opcode.SERVER_LOGIN_REDIRECT) // 0xA102
                        {
                            using var reader = new SRPacketReader(packet);
                            byte result = reader.ReadByte();
                            if (result == 0x01)
                            {
                                uint token = reader.ReadUInt32();
                                string agentIP = reader.ReadAscii();
                                ushort agentPort = reader.ReadUInt16();

                                BotLogger.Info("Proxy", $"Login Success! Redirecting to Agent: {agentIP}:{agentPort}");
                                _agentToken = token;
                                ProxyManager.Instance.SetNextTarget(agentIP, agentPort);
                                
                                using var writer = new SRPacketWriter(Opcode.SERVER_LOGIN_REDIRECT);
                                writer.WriteByte(0x01);
                                writer.WriteUInt32(token);
                                writer.WriteAscii("127.0.0.1");
                                writer.WriteUInt16((ushort)ProxyManager.Instance.LocalPort);
                                
                                var redirectedPacket = writer.Build();
                                BotLogger.Info("Proxy", $"Redirecting SRO Client to 127.0.0.1:{ProxyManager.Instance.LocalPort}");
                                
                                byte[] encBytes = _clientSecurity.FormatPacket(redirectedPacket);
                                await clientStream.WriteAsync(encBytes, 0, encBytes.Length);
                                continue;
                            }
                        }

                        if (packet.Opcode == Opcode.SERVER_LOGIN_ERROR)
                        {
                            using var reader = new SRPacketReader(packet);
                            byte result = reader.ReadByte();
                            if (result == 0x01) BotLogger.Info("Proxy", "[Agent] Authentication successful.");
                            else BotLogger.Error("Proxy", $"[Agent] Authentication failed: {result}");
                        }

                        try
                        {
                            _dispatcher?.Enqueue(packet);
                        }
                        catch (Exception ex)
                        {
                            BotLogger.Error("Proxy", $"Packet Handling Error (0x{packet.Opcode:X4}): {ex.Message}");
                        }

                        // v1.1.25: Don't relay Guard Challenges (handled by 'Instant Flush' bot handlers)
                        if (packet.Opcode == Opcode.SERVER_GUARD_CHALLENGE_1 || 
                            packet.Opcode == Opcode.SERVER_GUARD_CHALLENGE_2 ||
                            packet.Opcode == Opcode.SERVER_VPLUS_SEC_SYNC)
                        {
                            continue;
                        }

                        byte[] outData = _clientSecurity.FormatPacket(packet);
                        await clientStream.WriteAsync(outData, 0, outData.Length);
                    }
                    else
                    {
                        await Task.Delay(1); 
                    }
                    
                    foreach (var outBytes in _clientSecurity.GetOutgoingBytes())
                    {
                         await clientStream.WriteAsync(outBytes, 0, outBytes.Length);
                    }
                }
            }
            catch (Exception ex) 
            { 
                if (!_isClosing) BotLogger.Error("Proxy", $"S->C Error: {ex.Message}"); 
            }
        }

        public void SelectCharacter(string charName)
        {
             BotLogger.Info($"[Agent] Selecting Character: {charName}");
             using var writer = new SRPacketWriter(Opcode.CLIENT_CHARACTER_SELECTION_JOIN_REQUEST, true);
             writer.WriteAscii(charName);
             var packet = writer.Build();
             PacketDispatcher.TriggerGlobalPacket(packet, true);
             _serverConnection.SendPacket(packet);
        }
        
        public void ConfirmSpawn()
        {
            var packet = new SRPacket(Opcode.CLIENT_CHARACTER_CONFIRM_SPAWN, Array.Empty<byte>());
            PacketDispatcher.TriggerGlobalPacket(packet, true);
            _serverConnection.SendPacket(packet);
            BotLogger.Info("Proxy", "[Auto-Login] Character entered game world successfully.");
        }
        
        public void Stop()
        {
            _isClosing = true;
            _dispatcherCts?.Cancel();
            try { _gameClient?.Close(); } catch { }
            try { _serverConnection?.Disconnect(); } catch { }
            BotLogger.Info("Proxy", $"Session for Port {_targetPort} stopped.");
        }

        private void StartKeepaliveLoop()
        {
            _ = Task.Run(async () =>
            {
                BotLogger.Info("Proxy", "Starting Keepalive loop (0x2002 every 5s)...");
                while (IsActive && _dispatcherCts != null && !_dispatcherCts.IsCancellationRequested)
                {
                    try
                    {
                        // v1.6.7: Heartbeat suppression only during initial handshake
                        if (_leg1Initialized && _serverConnection != null && _serverConnection.IsConnected)
                        {
                            using var writer = new SRPacketWriter(Opcode.CLIENT_KEEPALIVE);
                            _serverConnection.SendPacket(writer.Build());
                        }
                    }
                    catch (Exception ex)
                    {
                        BotLogger.Warn("Proxy", $"Keepalive Error: {ex.Message}");
                    }
                    
                    try
                    {
                        await Task.Delay(5000, _dispatcherCts?.Token ?? CancellationToken.None);
                    }
                    catch (OperationCanceledException) { break; }
                }
            });
        }

        private static byte[] GetLocalMacAddress()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet ||
                         nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) &&
                        nic.GetPhysicalAddress().GetAddressBytes().Length == 6)
                    {
                        byte[] mac = nic.GetPhysicalAddress().GetAddressBytes();
                        BotLogger.Info("Proxy", $"[DEBUG] Using MAC: {BitConverter.ToString(mac)}");
                        return mac;
                    }
                }
            }
            catch (Exception ex)
            {
                BotLogger.Warn("Proxy", $"Could not read MAC address: {ex.Message}");
            }
            byte[] fallback = new byte[6];
            new Random().NextBytes(fallback);
            fallback[0] = (byte)(fallback[0] & 0xFE | 0x02); 
            BotLogger.Warn("Proxy", $"[DEBUG] Using fallback MAC: {BitConverter.ToString(fallback)}");
            return fallback;
        }
    }
}


