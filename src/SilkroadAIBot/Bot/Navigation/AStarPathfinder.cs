using System.Numerics;
using SilkroadAIBot.Data.Models.NavMesh;
using SilkroadAIBot.Data;

namespace SilkroadAIBot.Bot.Navigation
{
    public class AStarPathfinder
    {
        private readonly Dictionary<ushort, NavMeshTerrain> _loadedTerrains = new();

        public void AddTerrain(ushort regionID, NavMeshTerrain terrain)
        {
            _loadedTerrains[regionID] = terrain;
        }

        public List<Vector3> FindPath(Vector3 start, Vector3 goal, ushort regionID)
        {
            if (!_loadedTerrains.TryGetValue(regionID, out var terrain))
            {
                // v4.1.4: Try to load on-demand if DataManager is available
                var dataManager = DataManager.Instance;
                if (dataManager?.Navmesh != null)
                {
                    terrain = dataManager.Navmesh.GetTerrain(regionID);
                    if (terrain != null) _loadedTerrains[regionID] = terrain;
                }
                
                if (terrain == null) return new List<Vector3> { goal };
            }

            var startCell = FindCell(start, terrain);
            var goalCell = FindCell(goal, terrain);

            if (startCell == null || goalCell == null)
                return new List<Vector3> { goal };

            // Standard A* implementation
            var openSet = new PriorityQueue<NavCellExtended, float>();
            var closedSet = new HashSet<NavCellExtended>();
            var cameFrom = new Dictionary<NavCellExtended, NavCellExtended>();
            var gScore = new Dictionary<NavCellExtended, float>();
            var fScore = new Dictionary<NavCellExtended, float>();

            var startNode = new NavCellExtended(startCell, regionID);
            var goalNode = new NavCellExtended(goalCell, regionID);

            openSet.Enqueue(startNode, 0);
            gScore[startNode] = 0;
            fScore[startNode] = Heuristic(startNode, goalNode);

            while (openSet.Count > 0)
            {
                var current = openSet.Dequeue();
                if (current.Equals(goalNode))
                    return ReconstructPath(cameFrom, current, start, goal);

                closedSet.Add(current);

                foreach (var neighbor in GetNeighbors(current))
                {
                    if (closedSet.Contains(neighbor)) continue;

                    float tentativeGScore = gScore[current] + Distance(current, neighbor);

                    if (!gScore.ContainsKey(neighbor) || tentativeGScore < gScore[neighbor])
                    {
                        cameFrom[neighbor] = current;
                        gScore[neighbor] = tentativeGScore;
                        fScore[neighbor] = gScore[neighbor] + Heuristic(neighbor, goalNode);
                        openSet.Enqueue(neighbor, fScore[neighbor]);
                    }
                }
            }

            return new List<Vector3> { goal }; // Fallback
        }

        private NavCellQuad? FindCell(Vector3 pos, NavMeshTerrain terrain)
        {
            // Vector2 (X, Z) check
            return terrain.Cells.FirstOrDefault(c => 
                pos.X >= c.Rectangle.Min.X && pos.X <= c.Rectangle.Max.X &&
                pos.Z >= c.Rectangle.Min.Y && pos.Z <= c.Rectangle.Max.Y);
        }

        private IEnumerable<NavCellExtended> GetNeighbors(NavCellExtended node)
        {
            if (!_loadedTerrains.TryGetValue(node.RegionID, out var terrain)) yield break;

            // Internal edges
            foreach (var edge in terrain.InternalEdges)
            {
                if (edge.AssocCell[0] == terrain.Cells.IndexOf(node.Cell))
                {
                    if (edge.AssocCell[1] != 0xFFFF)
                        yield return new NavCellExtended(terrain.Cells[edge.AssocCell[1]], node.RegionID);
                }
                else if (edge.AssocCell[1] == terrain.Cells.IndexOf(node.Cell))
                {
                     yield return new NavCellExtended(terrain.Cells[edge.AssocCell[0]], node.RegionID);
                }
            }

            // Global edges (neighboring regions)
            foreach (var edge in terrain.GlobalEdges)
            {
                // This would require loading neighboring regions, keeping it local for now
            }
        }

        private float Heuristic(NavCellExtended a, NavCellExtended b) => Vector2.Distance(a.Center, b.Center);
        private float Distance(NavCellExtended a, NavCellExtended b) => Vector2.Distance(a.Center, b.Center);

        private List<Vector3> ReconstructPath(Dictionary<NavCellExtended, NavCellExtended> cameFrom, NavCellExtended current, Vector3 start, Vector3 goal)
        {
            var path = new List<Vector3> { goal };
            var node = current;
            while (cameFrom.TryGetValue(node, out node))
            {
                path.Add(new Vector3(node.Center.X, 0, node.Center.Y)); // Height is handled via WorldState
            }
            path.Reverse();
            return path;
        }

        private class NavCellExtended
        {
            public NavCellQuad Cell { get; }
            public ushort RegionID { get; }
            public Vector2 Center { get; }

            public NavCellExtended(NavCellQuad cell, ushort regionID)
            {
                Cell = cell;
                RegionID = regionID;
                Center = (cell.Rectangle.Min + cell.Rectangle.Max) / 2;
            }

            public override bool Equals(object? obj) => obj is NavCellExtended other && Cell == other.Cell && RegionID == other.RegionID;
            public override int GetHashCode() => HashCode.Combine(Cell, RegionID);
        }
    }
}
