using System;
using System.IO;
using System.Collections.Generic;
using System.Numerics;
using SilkroadAIBot.Data.Models.NavMesh;
using SilkroadAIBot.Bot;
using SRO.PK2API;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Data.Readers
{
    public class NavmeshReader
    {
        private Pk2Stream? _mapPk2;

        public NavmeshReader(Pk2Stream? mapPk2)
        {
            _mapPk2 = mapPk2;
        }

        public NavMeshTerrain? GetTerrain(ushort region)
        {
            if (_mapPk2 == null) return null;

            string path = $"nvmb\\{region}.nvm";
            var file = _mapPk2.GetFile(path);
            if (file == null) return null;

            byte[]? data = file.GetContent();
            if (data == null) return null;

            return ParseNvm(data);
        }

        public NavMeshTerrain ParseNvm(byte[] data)
        {
            try
            {
                using (var ms = new MemoryStream(data))
                using (var br = new BinaryReader(ms))
                {
                    char[] sigChars = br.ReadChars(12);
                    string signature = new string(sigChars);
                    if (!signature.StartsWith("JMXVNAV 1000"))
                    {
                        return new NavMeshTerrain { Heights = new float[97 * 97] };
                    }

                    var terrain = new NavMeshTerrain();
                    terrain.Signature = signature;

                    // 1. Tiles (96 * 96)
                    for (int i = 0; i < 9216; i++)
                    {
                        terrain.Tiles[i] = new NavMeshTile
                        {
                            CellID = (uint)i,
                            Flag = br.ReadUInt16(),
                            TextureID = br.ReadUInt16()
                        };
                    }

                    // 2. Heights (97 * 97)
                    for (int i = 0; i < 9409; i++)
                    {
                        terrain.Heights[i] = br.ReadSingle();
                    }

                    // 3. Objects
                    uint objCount = br.ReadUInt32();
                    for (int i = 0; i < objCount; i++)
                    {
                        var obj = new NavMeshObjectInstance
                        {
                            AssetID = br.ReadUInt32(),
                            Position = new Vector3(br.ReadSingle(), br.ReadSingle(), br.ReadSingle()),
                            Yaw = br.ReadSingle(),
                            RID = br.ReadUInt16(),
                            LocalUID = br.ReadUInt16()
                        };
                        
                        uint linkCount = br.ReadUInt32();
                        for (int j = 0; j < linkCount; j++)
                        {
                            obj.Links.Add(new LinkEdge { 
                                LinkedObjID = br.ReadUInt16(),
                                LinkedObjEdgeID = br.ReadUInt16(),
                                EdgeID = br.ReadUInt16()
                            });
                        }
                        terrain.Objects.Add(obj);
                    }

                    // 4. Cells
                    uint cellCount = br.ReadUInt32();
                    for (int i = 0; i < cellCount; i++)
                    {
                        var cell = new NavCellQuad();
                        cell.Rectangle = new NavRect {
                            Min = new Vector2(br.ReadSingle(), br.ReadSingle()),
                            Max = new Vector2(br.ReadSingle(), br.ReadSingle())
                        };
                        
                        ushort objIdxCount = br.ReadUInt16();
                        cell.ObjectIndices = new ushort[objIdxCount];
                        for (int j = 0; j < objIdxCount; j++) cell.ObjectIndices[j] = br.ReadUInt16();
                        
                        terrain.Cells.Add(cell);
                    }

                    // 5. Global Edges
                    uint globalEdgeCount = br.ReadUInt32();
                    for (int i = 0; i < globalEdgeCount; i++)
                    {
                        var edge = new NavEdgeGlobal {
                            Min = new Vector2(br.ReadSingle(), br.ReadSingle()),
                            Max = new Vector2(br.ReadSingle(), br.ReadSingle()),
                            Flag = br.ReadByte(),
                            AssocDirection = br.ReadBytes(2),
                            AssocCell = new ushort[] { br.ReadUInt16(), br.ReadUInt16() },
                            AssocRegion = new ushort[] { br.ReadUInt16(), br.ReadUInt16() }
                        };
                        terrain.GlobalEdges.Add(edge);
                    }

                    // 6. Internal Edges
                    uint internalEdgeCount = br.ReadUInt32();
                    for (int i = 0; i < internalEdgeCount; i++)
                    {
                        var edge = new NavEdgeInternal {
                            Min = new Vector2(br.ReadSingle(), br.ReadSingle()),
                            Max = new Vector2(br.ReadSingle(), br.ReadSingle()),
                            Flag = br.ReadByte(),
                            AssocDirection = br.ReadBytes(2),
                            AssocCell = new ushort[] { br.ReadUInt16(), br.ReadUInt16() }
                        };
                        terrain.InternalEdges.Add(edge);
                    }
                    
                    return terrain;
                }
            }
            catch (Exception ex)
            {
                BotLogger.Error("NavmeshReader", $"NVM Parse Error: {ex.Message}");
                return new NavMeshTerrain { Heights = new float[97 * 97] };
            }
        }
    }
}
