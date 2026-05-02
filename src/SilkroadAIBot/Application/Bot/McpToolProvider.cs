using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SilkroadAIBot.Application.Interfaces;
using SilkroadAIBot.Domain.Entities;
using System.Text.Json;

namespace SilkroadAIBot.Application.Bot
{
    public class McpToolProvider : IMcpToolProvider
    {
        private readonly IWorldStateRepository _worldState;
        private readonly IEntityRepository _entityRepo;
        private readonly IPacketSender _sender;
        private readonly IBotController _botController;

        public McpToolProvider(
            IWorldStateRepository worldState, 
            IEntityRepository entityRepo, 
            IPacketSender sender,
            IBotController botController)
        {
            _worldState = worldState;
            _entityRepo = entityRepo;
            _sender = sender;
            _botController = botController;
        }

        public IReadOnlyList<McpTool> GetTools()
        {
            var tools = new List<McpTool>();

            tools.Add(new McpTool(
                "get_world_state",
                "Returns a full snapshot of the current character state and all nearby entities.",
                JsonSerializer.Serialize(new { type = "object", properties = new { } }),
                async (args, ct) => {
                    var character = _worldState.GetCharacter();
                    var result = new {
                        character = new {
                            name = character.Name,
                            level = character.Level,
                            hp = character.HP,
                            maxHp = character.HPMax,
                            mp = character.MP,
                            maxMp = character.MPMax,
                            position = character.Position
                        },
                        entities = _worldState.NearbyEntities.Select(e => new {
                            uid = e.UniqueID,
                            modelId = e.ModelID,
                            name = e.Name,
                            position = e.Position,
                            distance = character.Position.DistanceTo(e.Position)
                        }).ToList()
                    };
                    return JsonSerializer.Serialize(result);
                }
            ));

            tools.Add(new McpTool(
                "move_to",
                "Moves the character to a specific X, Y coordinate in the current region.",
                JsonSerializer.Serialize(new {
                    type = "object",
                    properties = new {
                        x = new { type = "number", description = "Target X coordinate" },
                        y = new { type = "number", description = "Target Y coordinate" }
                    },
                    required = new[] { "x", "y" }
                }),
                async (args, ct) => {
                    var data = JsonSerializer.Deserialize<JsonElement>(args);
                    float x = data.GetProperty("x").GetSingle();
                    float y = data.GetProperty("y").GetSingle();
                    var charPos = _worldState.GetCharacter().Position;
                    var targetPos = charPos with { X = x, Y = y };
                    _sender.SendMovement(targetPos);
                    return JsonSerializer.Serialize(new { status = "success", message = $"Moving to {x}, {y}" });
                }
            ));

            tools.Add(new McpTool(
                "cast_skill",
                "Casts a skill on a target entity.",
                JsonSerializer.Serialize(new {
                    type = "object",
                    properties = new {
                        skillId = new { type = "integer", description = "The ID of the skill to cast" },
                        targetUid = new { type = "integer", description = "The UniqueID of the target entity" }
                    },
                    required = new[] { "skillId", "targetUid" }
                }),
                async (args, ct) => {
                    var data = JsonSerializer.Deserialize<JsonElement>(args);
                    uint skillId = data.GetProperty("skillId").GetUInt32();
                    uint targetUid = data.GetProperty("targetUid").GetUInt32();
                    _sender.SendSelectTarget(targetUid);
                    await Task.Delay(100, ct);
                    _sender.SendCastSkill(skillId, targetUid);
                    return JsonSerializer.Serialize(new { status = "success", message = $"Casting skill {skillId} on {targetUid}" });
                }
            ));

            return tools.AsReadOnly();
        }

        public async Task<object> CallToolAsync(string name, Dictionary<string, object> arguments)
        {
            var tools = GetTools();
            var tool = tools.FirstOrDefault(t => t.Name == name);
            if (tool == null)
            {
                throw new Exception($"Tool '{name}' not found");
            }

            string jsonArgs = JsonSerializer.Serialize(arguments);
            return await tool.Handler(jsonArgs, CancellationToken.None);
        }
    }
}
