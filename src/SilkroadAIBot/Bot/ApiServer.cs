using System;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using SilkroadAIBot.Networking;
using SilkroadAIBot.Domain.Entities;
using SilkroadAIBot.Core.Helpers;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using SilkroadAIBot.Core.Configuration;
using SilkroadAIBot.Core.Memory;

namespace SilkroadAIBot.Bot
{
    /// <summary>
    /// A local REST API that allows external tools (like AI Agents) 
    /// to control the SilkroadAIBot and read the world state.
    /// </summary>
    public class ApiServer
    {
        private HttpListener? _listener;
        private readonly Application.Interfaces.IWorldStateRepository _worldState;
        private PacketSender? _packetSender;
        private bool _isRunning;

        public ApiServer(Application.Interfaces.IWorldStateRepository worldState)
        {
            _worldState = worldState;
        }

        public void SetPacketSender(PacketSender sender)
        {
            _packetSender = sender;
        }

        public void Start(int port = 5000)
        {
            if (_isRunning) return;

            try
            {
                BotLogger.Info("API", $"Starting AI Control Server on port {port}...");
                _listener = new HttpListener();
                _listener.Prefixes.Add($"http://localhost:{port}/");
                
                BotLogger.Debug("API", "Attempting to bind HttpListener...");
                _listener.Start();
                BotLogger.Debug("API", "HttpListener bound successfully.");
                
                _isRunning = true;
                
                Task.Run(() => ListenAsync());
                string msg = $"AI Control Interface active on http://localhost:{port}";
                BotLogger.Info("API", msg);
                Console.WriteLine($"[API] {msg}");
            }
            catch (Exception ex)
            {
                string err = $"Failed to start API Server: {ex.Message}";
                BotLogger.Error("API", err);
                Console.WriteLine($"[API] ERROR: {err}");
                // Important: Don't let API failure crash the whole bot, but log it clearly
            }
        }

        public void Stop()
        {
            if (!_isRunning) return;
            _isRunning = false;
            _listener?.Stop();
            _listener?.Close();
            BotLogger.Info("API", "AI Control Interface stopped.");
        }

        private async Task ListenAsync()
        {
            while (_isRunning && _listener != null && _listener.IsListening)
            {
                try
                {
                    var context = await _listener.GetContextAsync();
                    _ = Task.Run(() => HandleRequest(context));
                }
                catch (HttpListenerException) { /* Ignored on stop */ }
                catch (Exception ex)
                {
                    BotLogger.Error("API", $"Listener error: {ex.Message}");
                }
            }
        }

        public Func<string?, int?, System.Diagnostics.Process?>? LaunchHandler { get; set; }
        public Func<bool>? ClientStatusHandler { get; set; }

        private async Task HandleRequest(HttpListenerContext context)
        {
            var req = context.Request;
            var res = context.Response;
            res.ContentType = "application/json";
            
            // CORS headers for external web UIs
            res.AddHeader("Access-Control-Allow-Origin", "*");
            res.AddHeader("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            res.AddHeader("Access-Control-Allow-Headers", "Content-Type");

            if (req.HttpMethod == "OPTIONS")
            {
                res.StatusCode = 200;
                res.Close();
                return;
            }

            try
            {
                string path = req.Url?.AbsolutePath.ToLower() ?? "/";
                string responseBody = "{}";

                if (req.HttpMethod == "GET")
                {
                    if (path == "/status")
                    {
                        var status = new
                        {
                            serverConnected = _packetSender != null && _packetSender.IsConnected,
                            clientLinked = SilkroadAIBot.Bot.IPCManager.Instance != null && SilkroadAIBot.Bot.IPCManager.Instance.IsLinked,
                            clientRunning = ClientStatusHandler?.Invoke() ?? false,
                            characterInWorld = _worldState.GetCharacter().UniqueID != 0,
                            characterName = _worldState.GetCharacter().Name
                        };
                        responseBody = JsonSerializer.Serialize(status);
                    }
                    else if (path == "/state")
                    {
                        var character = _worldState.GetCharacter();
                        var state = new
                        {
                            character = character.UniqueID != 0 ? new {
                                name = character.Name,
                                level = character.Level,
                                hp = character.HP,
                                maxHp = character.HPMax,
                                mp = character.MP,
                                maxMp = character.MPMax,
                                gold = character.Gold,
                                sp = character.SkillPoints,
                                position = new {
                                    region = character.Position.Region,
                                    x = character.Position.X,
                                    y = character.Position.Y,
                                    z = character.Position.Z
                                }
                            } : null,
                            entities = _worldState.NearbyEntities.Select(e => new {
                                uid = e.UniqueID,
                                id = e.ModelID,
                                name = e.Name,
                                distance = character.UniqueID != 0 ? e.Position.DistanceTo(character.Position) : 0,
                                type = e.GetType().Name,
                                isPlayer = (e is SRPlayer)
                            }).ToList()
                        };
                        responseBody = JsonSerializer.Serialize(state);
                    }
                    else if (path == "/inventory")
                    {
                        var charData = _worldState.GetCharacter();
                        var inv = charData.Inventory.Select(item => new {
                            slot = item.Slot,
                            id = item.ModelID,
                            name = item.Name,
                            quantity = item.Amount
                        });
                        responseBody = JsonSerializer.Serialize(new { inventory = inv });
                    }
                    else if (path == "/skills")
                    {
                        var charData = _worldState.GetCharacter();
                        var skills = charData.LearnedSkills.Select(s => new {
                            id = s.SkillID,
                            level = s.Level
                        });
                        responseBody = JsonSerializer.Serialize(new { skills = skills });
                    }
                    else if (path == "/logs")
                    {
                        responseBody = JsonSerializer.Serialize(new { logs = _worldState.ActionLogs });
                    }
                    else
                    {
                        res.StatusCode = 404;
                        responseBody = JsonSerializer.Serialize(new { error = "Not found" });
                    }
                }
                else if (req.HttpMethod == "POST")
                {
                    using var reader = new System.IO.StreamReader(req.InputStream, req.ContentEncoding);
                    string body = await reader.ReadToEndAsync();

                    if (path == "/launch")
                    {
                        // ... existing launch code
                    }
                    else if (path == "/login")
                    {
                        // ... existing login code
                    }
                    else 
                    {
                        // v1.3.0: Priority Routing - Use Proxy Session if available, fallback to Clientless
                        var proxy = SilkroadAIBot.Proxy.ProxyManager.Instance;
                        PacketSender? currentSender = null;

                        if (proxy != null && proxy.ActiveSession != null && proxy.ActiveSession.ServerConnection != null)
                        {
                            // Create a temporary sender tied to the real client-server tunnel
                            currentSender = new PacketSender(_worldState, () => proxy.ActiveSession?.ServerConnection);
                        }
                        else if (_packetSender != null && _packetSender.IsConnected)
                        {
                            currentSender = _packetSender;
                        }

                        if (currentSender == null)
                        {
                            res.StatusCode = 400;
                            responseBody = JsonSerializer.Serialize(new { error = "No active connection (Proxy or Clientless)" });
                        }
                        else
                        {
                            if (path == "/move")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, float>>(body);
                                if (data != null && data.ContainsKey("x") && data.ContainsKey("y"))
                                {
                                    var character = _worldState.GetCharacter();
                                    var pos = character.Position;
                                    // v1.1.18: Use provided X,Y. If Region is 0, default to Downhang (24103)
                                    var targetPos = pos with { X = data["x"], Y = data["y"] };
                                    if (targetPos.Region == 0) targetPos = targetPos with { Region = 24103 }; 
                                    
                                    // Send to Game Server
                                    currentSender.SendMovement(targetPos);
                                    
                                    // Sync with Client UI via IPC
                                    _ = Task.Run(async () => {
                                        var ipc = SilkroadAIBot.Bot.IPCManager.Instance;
                                        if (ipc != null) await ipc.SendCommand($"MOVE_TO:{pos.X:F2},{pos.Y:F2}");
                                    });

                                    responseBody = JsonSerializer.Serialize(new { status = "Moving", region = pos.Region });
                                }
                            }
                            else if (path == "/attack")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, uint>>(body);
                                if (data != null && data.ContainsKey("uid"))
                                {
                                    uint uid = data["uid"];
                                    currentSender.SendSelectTarget(uid);
                                    
                                    // v1.1.19: Sync Target with Client
                                    _ = Task.Run(async () => {
                                        var ipc = SilkroadAIBot.Bot.IPCManager.Instance;
                                        if (ipc != null) await ipc.SendCommand($"SET_TARGET:{uid}");
                                    });

                                    await Task.Delay(200);
                                    currentSender.SendBasicAttack(uid);
                                    responseBody = JsonSerializer.Serialize(new { status = "Attacking target", target = uid });
                                }
                            }
                            else if (path == "/cast")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, uint>>(body);
                                if (data != null && data.ContainsKey("skillId") && data.ContainsKey("uid"))
                                {
                                    uint uid = data["uid"];
                                    uint sid = data["skillId"];
                                    
                                    currentSender.SendSelectTarget(uid);
                                    
                                    // v1.1.19: Sync Target with Client
                                    _ = Task.Run(async () => {
                                        var ipc = SilkroadAIBot.Bot.IPCManager.Instance;
                                        if (ipc != null) await ipc.SendCommand($"SET_TARGET:{uid}");
                                    });

                                    await Task.Delay(200);
                                    currentSender.SendCastSkill(sid, uid);
                                    responseBody = JsonSerializer.Serialize(new { status = "Casting skill", skill = sid, target = uid });
                                }
                            }
                            else if (path == "/chat")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                                if (data != null && data.ContainsKey("type") && data.ContainsKey("message"))
                                {
                                    byte type = byte.Parse(data["type"].ToString()!);
                                    string msg = data["message"].ToString()!;
                                    string target = data.ContainsKey("target") ? data["target"].ToString()! : "";
                                    currentSender.SendChat(type, msg, target);
                                    responseBody = JsonSerializer.Serialize(new { status = "Message sent" });
                                }
                            }
                            else if (path == "/action")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, uint>>(body);
                                if (data != null && data.ContainsKey("uid"))
                                {
                                    currentSender.SendEntityAction(data["uid"]);
                                    responseBody = JsonSerializer.Serialize(new { status = "Action sent" });
                                }
                            }
                            else if (path == "/useitem")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, byte>>(body);
                                if (data != null && data.ContainsKey("slot"))
                                {
                                    currentSender.SendUseItem(data["slot"]);
                                    responseBody = JsonSerializer.Serialize(new { status = "Item use sent", slot = data["slot"] });
                                }
                            }
                            else if (path == "/itemmove")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                                if (data != null && data.ContainsKey("action") && data.ContainsKey("src") && data.ContainsKey("dest"))
                                {
                                    byte action = byte.Parse(data["action"].ToString()!);
                                    byte src = byte.Parse(data["src"].ToString()!);
                                    byte dest = byte.Parse(data["dest"].ToString()!);
                                    ushort quantity = data.ContainsKey("quantity") ? ushort.Parse(data["quantity"].ToString()!) : (ushort)0;
                                    currentSender.SendItemMove(action, src, dest, quantity);
                                    responseBody = JsonSerializer.Serialize(new { status = "Item move sent", action, src, dest });
                                }
                            }
                            else if (path == "/resurrect")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, byte>>(body);
                                if (data != null && data.ContainsKey("type"))
                                {
                                    currentSender.SendResurrection(data["type"]);
                                    responseBody = JsonSerializer.Serialize(new { status = "Resurrection sent", type = data["type"] });
                                }
                            }
                            else if (path == "/party")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                                if (data != null && data.ContainsKey("action"))
                                {
                                    string action = data["action"].ToString()!.ToLower();
                                    if (action == "create" && data.ContainsKey("uid") && data.ContainsKey("settings"))
                                        currentSender.SendPartyCreate(uint.Parse(data["uid"].ToString()!), byte.Parse(data["settings"].ToString()!));
                                    else if (action == "invite" && data.ContainsKey("uid"))
                                        currentSender.SendPartyInvite(uint.Parse(data["uid"].ToString()!));
                                    else if (action == "leave")
                                        currentSender.SendPartyLeave();
                                    else if (action == "kick" && data.ContainsKey("jid"))
                                        currentSender.SendPartyKick(uint.Parse(data["jid"].ToString()!));
                                    
                                    responseBody = JsonSerializer.Serialize(new { status = $"Party action {action} sent" });
                                }
                            }
                            else if (path == "/exchange")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                                if (data != null && data.ContainsKey("action"))
                                {
                                    string action = data["action"].ToString()!.ToLower();
                                    if (action == "start" && data.ContainsKey("uid"))
                                        currentSender.SendExchangeStart(uint.Parse(data["uid"].ToString()!));
                                    else if (action == "approve")
                                        currentSender.SendExchangeApprove();
                                    else if (action == "confirm")
                                        currentSender.SendExchangeConfirm();
                                    else if (action == "cancel")
                                        currentSender.SendExchangeCancel();

                                    responseBody = JsonSerializer.Serialize(new { status = $"Exchange action {action} sent" });
                                }
                            }
                            else if (path == "/stall")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                                if (data != null && data.ContainsKey("action"))
                                {
                                    string action = data["action"].ToString()!.ToLower();
                                    if (action == "create" && data.ContainsKey("name"))
                                        currentSender.SendStallCreate(data["name"].ToString()!);
                                    else if (action == "destroy")
                                        currentSender.SendStallDestroy();
                                    else if (action == "leave")
                                        currentSender.SendStallLeave();
                                    else if (action == "talk" && data.ContainsKey("uid"))
                                        currentSender.SendStallTalk(uint.Parse(data["uid"].ToString()!));
                                    else if (action == "buy" && data.ContainsKey("slot"))
                                        currentSender.SendStallBuy(byte.Parse(data["slot"].ToString()!));

                                    responseBody = JsonSerializer.Serialize(new { status = $"Stall action {action} sent" });
                                }
                            }
                            else if (path == "/teleport")
                            {
                                var data = JsonSerializer.Deserialize<Dictionary<string, object>>(body);
                                if (data != null && data.ContainsKey("npcUid") && data.ContainsKey("type"))
                                {
                                    uint npcUid = uint.Parse(data["npcUid"].ToString()!);
                                    byte type = byte.Parse(data["type"].ToString()!);
                                    uint teleId = data.ContainsKey("teleportId") ? uint.Parse(data["teleportId"].ToString()!) : 0;
                                    byte guideType = data.ContainsKey("guideType") ? byte.Parse(data["guideType"].ToString()!) : (byte)0;
                                    
                                    currentSender.SendTeleportUse(npcUid, type, teleId, guideType);
                                    responseBody = JsonSerializer.Serialize(new { status = "Teleport sent" });
                                }
                            }
                            else
                            {
                                res.StatusCode = 404;
                                responseBody = JsonSerializer.Serialize(new { error = "Command not found" });
                            }
                        }
                    }
                }

                byte[] buffer = Encoding.UTF8.GetBytes(responseBody);
                res.ContentLength64 = buffer.Length;
                await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            catch (Exception ex)
            {
                res.StatusCode = 500;
                byte[] buffer = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new { error = ex.Message }));
                await res.OutputStream.WriteAsync(buffer, 0, buffer.Length);
            }
            finally
            {
                res.Close();
            }
        }
    }
}
