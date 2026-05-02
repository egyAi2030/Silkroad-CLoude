namespace SilkroadAIBot.Domain.Entities;

public record SRCoord(ushort Region, float X, float Y, float Z)
{
    public SRCoord() : this(0, 0, 0, 0) { }
    public override string ToString() => $"[R:{Region} X:{X:F1} Y:{Y:F1} Z:{Z:F1}]";

    /// <summary>Absolute world X coordinate calculated from region and local X.</summary>
    public float WorldX => ((Region & 0xFF) - 135) * 192 + X / 10;

    /// <summary>Absolute world Y coordinate calculated from region and local Y.</summary>
    public float WorldY => (((Region >> 8) & 0xFF) - 92) * 192 + Y / 10;

    /// <summary>Euclidean 2D distance (ignores Z height) to another coord.</summary>
    public float DistanceTo(SRCoord other)
    {
        if (Region != other.Region) return 1000000; 
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
