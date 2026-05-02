using System.Numerics;

namespace SilkroadAIBot.Data.Models.NavMesh
{
    public struct NavRect
    {
        public Vector2 Min;
        public Vector2 Max;
    }

    public class NavCellQuad
    {
        public NavRect Rectangle { get; set; }
        public ushort[] ObjectIndices { get; set; }
    }

    public class NavEdgeGlobal
    {
        public Vector2 Min { get; set; }
        public Vector2 Max { get; set; }
        public byte Flag { get; set; }
        public byte[] AssocDirection { get; set; }
        public ushort[] AssocCell { get; set; }
        public ushort[] AssocRegion { get; set; }
    }

    public class NavEdgeInternal
    {
        public Vector2 Min { get; set; }
        public Vector2 Max { get; set; }
        public byte Flag { get; set; }
        public byte[] AssocDirection { get; set; }
        public ushort[] AssocCell { get; set; }
    }

    public class NavMeshTile
    {
        public uint CellID { get; set; }
        public ushort Flag { get; set; }
        public ushort TextureID { get; set; }
    }

    public class NavMeshTerrain
    {
        public string Signature { get; set; }
        public ushort RegionID { get; set; }
        public List<NavMeshObjectInstance> Objects { get; set; } = new();
        public List<NavCellQuad> Cells { get; set; } = new();
        public List<NavEdgeGlobal> GlobalEdges { get; set; } = new();
        public List<NavEdgeInternal> InternalEdges { get; set; } = new();
        public NavMeshTile[] Tiles { get; set; } = new NavMeshTile[96 * 96];
        public float[] Heights { get; set; } = new float[97 * 97];
    }

    public class NavMeshObjectInstance
    {
        public uint AssetID { get; set; }
        public Vector3 Position { get; set; }
        public ushort Type { get; set; }
        public float Yaw { get; set; }
        public ushort LocalUID { get; set; }
        public ushort RID { get; set; }
        public List<LinkEdge> Links { get; set; } = new();
    }

    public struct LinkEdge
    {
        public ushort LinkedObjID;
        public ushort LinkedObjEdgeID;
        public ushort EdgeID;
    }
}
