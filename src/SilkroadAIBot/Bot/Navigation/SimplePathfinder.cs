using System;
using System.Collections.Generic;
using System.Linq;
using SilkroadAIBot.Domain.Entities;

namespace SilkroadAIBot.Bot.Navigation
{
    public class SimplePathfinder
    {
        // Future: Load NavMesh or WalkMap here
        
        public static List<SRCoord> CalculatePath(SRCoord start, SRCoord end)
        {
            // Currently implements direct pathing.
            // In a real implementation, this would use A* on a grid or navmesh.
            
            var path = new List<SRCoord>();
            path.Add(start);
            
            // Simple direct line check (placeholder for collision detection)
            // If distance is too far, maybe split it? 
            // For now, Silkroad movement is often point-to-point within visibility.
            
            if (start.DistanceTo(end) > 100)
            {
                // Split long paths for safety if no mesh
                var midPoint = new SRCoord(
                    start.Region, 
                    (start.X + end.X) / 2, 
                    (start.Y + end.Y) / 2, 
                    (start.Z + end.Z) / 2
                );
                path.Add(midPoint);
            }
            
            path.Add(end);
            return path;
        }
    }
}


