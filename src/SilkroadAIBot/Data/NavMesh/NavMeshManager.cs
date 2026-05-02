using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using SilkroadAIBot.Data.Models.NavMesh;
using SilkroadAIBot.Data.Readers;
using SilkroadAIBot.Core.Helpers;

namespace SilkroadAIBot.Data.NavMesh
{
    /// <summary>
    /// v1.4.0 — Manages global NavMesh data, providing terrain info and pathfinding services.
    /// Orchestrates between the PK2 data source and the A* Pathfinder.
    /// </summary>
    public class NavMeshManager
    {
        private readonly DataManager _dataManager;
        private readonly ConcurrentDictionary<ushort, NavMeshTerrain> _terrainCache = new();
        private readonly ConcurrentDictionary<ushort, bool> _loadingRegions = new();

        public NavMeshManager(DataManager dataManager)
        {
            _dataManager = dataManager;
        }

        /// <summary>
        /// Retrieves or loads the NavMesh terrain for a specific region.
        /// </summary>
        public async Task<NavMeshTerrain?> GetTerrainAsync(ushort regionID)
        {
            if (_terrainCache.TryGetValue(regionID, out var terrain))
                return terrain;

            if (_loadingRegions.TryAdd(regionID, true))
            {
                try 
                {
                    BotLogger.Info("NavMesh", $"Loading navigation data for Region {regionID}...");
                    var data = await Task.Run(() => LoadRegionNvm(regionID));
                    if (data != null)
                    {
                        data.RegionID = regionID;
                        _terrainCache[regionID] = data;
                        return data;
                    }
                }
                finally
                {
                    _loadingRegions.TryRemove(regionID, out _);
                }
            }

            return null;
        }

        private NavMeshTerrain? LoadRegionNvm(ushort regionID)
        {
            try 
            {
                byte x = (byte)(regionID & 0xFF);
                byte y = (byte)((regionID >> 8) & 0xFF);
                
                string path = $"Map/{x}/{y}.nvm";
                var pk2Stream = _dataManager.GetPk2File(path, "Map.pk2");
                
                if (pk2Stream == null)
                {
                    // Fallback to Media.pk2 (some private servers store it there)
                    pk2Stream = _dataManager.GetPk2File(path, "Media.pk2");
                    if (pk2Stream == null) return null;
                }

                var pk2File = pk2Stream.GetFile(path);
                if (pk2File == null) return null;

                var data = pk2File.GetContent();
                if (data == null || data.Length < 12) return null;

                return new SilkroadAIBot.Data.Readers.NavmeshReader(null).ParseNvm(data);
            }
            catch (Exception ex)
            {
                BotLogger.Error("NavMesh", $"Failed to load NavMesh for Region {regionID}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Returns the exact height at a specific world coordinate within a region.
        /// </summary>
        public float GetHeight(ushort regionID, float x, float z)
        {
            if (_terrainCache.TryGetValue(regionID, out var terrain))
            {
                // Tiles are 96x96. Region size is 1920.0 units.
                // grid resolution is 1920 / 96 = 20 units.
                int gridX = (int)(x / 20.0f);
                int gridZ = (int)(z / 20.0f);

                if (gridX >= 0 && gridX < 97 && gridZ >= 0 && gridZ < 97)
                {
                    return terrain.Heights[gridZ * 97 + gridX];
                }
            }
            return 0.0f;
        }

        public void ClearCache()
        {
            _terrainCache.Clear();
            _loadingRegions.Clear();
        }
    }
}
