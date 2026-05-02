using System;
using System.Collections.Generic;
using System.Numerics;
using SilkroadAIBot.Domain.Entities;
using System.Linq;

namespace SilkroadAIBot.Bot.Navigation
{
    public class Pathfinder
    {
        private static readonly AStarPathfinder _aStar = new();

        /// <summary>
        /// Finds a path between start and end coordinates using NavMesh-based A*.
        /// </summary>
        public static List<SRCoord> FindPath(SRCoord startCoord, SRCoord endCoord)
        {
            var start = new Vector3(startCoord.X, 0, startCoord.Y);
            var goal = new Vector3(endCoord.X, 0, endCoord.Y);
            
            // regionID is needed for NVM lookup
            var path = _aStar.FindPath(start, goal, startCoord.Region);

            return path.Select(p => new SRCoord(startCoord.Region, (float)p.X, (float)p.Z, 0)).ToList();
        }

        public static void RegisterTerrain(ushort regionID, Data.Models.NavMesh.NavMeshTerrain terrain)
        {
            _aStar.AddTerrain(regionID, terrain);
        }
    }
}


