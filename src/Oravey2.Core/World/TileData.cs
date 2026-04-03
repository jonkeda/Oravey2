namespace Oravey2.Core.World;

public readonly record struct TileData(
    SurfaceType Surface,
    byte HeightLevel,
    byte WaterLevel,
    int StructureId,
    TileFlags Flags,
    byte VariantSeed)
{
    public static readonly TileData Empty = default;

    public bool IsWalkable => Flags.HasFlag(TileFlags.Walkable);
    public bool HasWater => WaterLevel > HeightLevel;
    public int WaterDepth => HasWater ? WaterLevel - HeightLevel : 0;

    /// <summary>
    /// Maps this tile to a legacy TileType for backward compatibility.
    /// </summary>
    public TileType LegacyTileType
    {
        get
        {
            if (HasWater) return TileType.Water;
            if (StructureId != 0 && !IsWalkable) return TileType.Wall;
            return Surface switch
            {
                SurfaceType.Asphalt or SurfaceType.Concrete => TileType.Road,
                SurfaceType.Rock => TileType.Rubble,
                SurfaceType.Metal => IsWalkable ? TileType.Road : TileType.Wall,
                _ => IsWalkable ? TileType.Ground : TileType.Empty
            };
        }
    }
}
