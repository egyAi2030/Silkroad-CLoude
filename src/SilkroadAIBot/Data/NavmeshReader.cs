using System;
using System.IO;
using System.Collections.Generic;
using SilkroadAIBot.Core.Helpers;
using SilkroadAIBot.Domain.Entities;
using SRO.PK2API;

namespace SilkroadAIBot.Data
{
    public class NavmeshReader
    {
        private Pk2Stream _mapPk2;
        private Dictionary<ushort, float[,]> _heightCache = new Dictionary<ushort, float[,]>();
        
        #region AINavData Advanced NavMesh Structures
        public struct RefCell
        {
            public ushort RefEdgeIndex0;
            public ushort RefEdgeIndex1;
        }

        public struct RefBlockLink
        {
            public uint ID;
            public ushort CellID;
            public ushort LinkedObjID;
            public ushort LinkedObjRefEdgeIndex;
        }

        public struct RefBlock 
        {
            public uint Index;
            public uint CellCount;
            public uint EdgeCount;
            public RefCell[] CellLookupTable;
            public uint LinkCount;
            public RefBlockLink[] Links;
        }

        public struct RefDungeon 
        {
            public ushort RegionID;
            public uint BlockCount;
            public RefBlock[] Blocks;
            public ushort[] BlockLookupTable;
        }

        public struct Vector3
        {
           public float X;
           public float Y;
           public float Z;
        }

        public struct SimpleDungeonBlock
        {
            public uint EdgeCount;
            public Vector3[] EdgeCenterPoints;
        }

        public struct SimpleDungeonData
        {
           public ushort RegionID;
           public uint BlockCount;
           public SimpleDungeonBlock[] Blocks;
        }

        public struct AI_NAVIGATION 
        {
            public byte Version;
            public uint SimpleDungeonDataOffset;
            public RefDungeon refDungeon;   
            public SimpleDungeonData simpleDungeonData;
        }
        #endregion

        // SRO Sector Constants
        private const int SECTOR_SIZE = 1920; // 192.0 units per sector? Or 100? Varies.
                                              // Actually standard is 256x256 units but split into blocks? 
                                              // Let's stick to reading the BMS file structure.
                                              
        public NavmeshReader(Pk2Stream mapPk2)
        {
            _mapPk2 = mapPk2;
        }

        public float GetHeight(ushort region, float x, float y)
        {
            // 1. Check cache
            if (!_heightCache.TryGetValue(region, out var heightMap))
            {
                heightMap = LoadSector(region);
                if (heightMap != null)
                {
                    _heightCache[region] = heightMap;
                }
            }

            if (heightMap == null) return 0.0f; // Default ground

            // SRO standard region size is 1920.0 units.
            // SRO heightmap grid is usually 96x96 tiles (starting at 0,0).
            // This means there are 97 vertices in each dimension.
            // Grid cell size = 1920 / 96 = 20.0 units.

            // x and y within the region [0, 1920).
            // In SRO, "y" in 2D is often the North/South axis, which maps to array Z or Y?
            // Usually array[x, y].
            
            float cellX = x / 20.0f;
            float cellY = y / 20.0f;
            
            int x1 = (int)cellX;
            int y1 = (int)cellY;
            
            // Clamp to valid grid range [0, 96]
            // Note: If x is 1920, x1=96. We need vertices x1 and x1+1.
            // If we only have 97 vertices (0..96), max index is 96.
            
            if (x1 < 0) x1 = 0; if (x1 > 95) x1 = 95;
            if (y1 < 0) y1 = 0; if (y1 > 95) y1 = 95;

            // Simple nearest/floor for now
            return heightMap[x1, y1];
            
            // TODO: Implement Bilinear Interpolation for smoother movement
            // float dx = cellX - x1;
            // float dy = cellY - y1;
            // ...
        }

        private float[,]? LoadSector(ushort region)
        {
            try 
            {
                byte xSector = (byte)(region & 0xFF);
                byte ySector = (byte)((region >> 8) & 0xFF);
                
                string path = $"Map/{xSector}/{ySector}.bms";
                var data = _mapPk2.GetFile(path)?.GetContent();
                
                if (data == null) 
                {
                    // Attempt "Data/Map" prefix just in case structure differs
                    path = $"Data/Map/{xSector}/{ySector}.bms";
                    data = _mapPk2.GetFile(path)?.GetContent();
                    if (data == null) return null;
                }

                using (var reader = new BinaryReader(new MemoryStream(data)))
                {
                    // Basic BMS Header Parsing
                    // 0x00: Version (4)
                    // 0x04: Map Type/Attr (4)
                    // 0x08: Unknown (4)
                    // 0x0C: Unknown (4)
                    // 0x10: Start of Height Data (97x97 floats) or different based on version
                    
                    int version = reader.ReadInt32();
                    reader.ReadInt32(); 
                    reader.ReadInt32(); 
                    reader.ReadInt32();

                    // Read 97x97 floats
                    // 97 * 97 * 4 bytes = 37636 bytes. 
                    // File size check?
                    
                    float[,] map = new float[97, 97];
                    for (int x = 0; x < 97; x++)
                    {
                         for (int y = 0; y < 97; y++)
                         {
                             map[x, y] = reader.ReadSingle(); 
                         }
                    }
                    return map;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[NavmeshReader] Error loading sector {region}: {ex.Message}");
                return null;
            }
        }
        
        private string GetBmsPath(ushort region)
        {
             // Not used directly logic moved to LoadSector
             return "";
        }
    }
}


