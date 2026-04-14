using Oravey2.Contracts.Spatial;

namespace Oravey2.MapGen.Generation;

public sealed class TownMapResult
{
    public TownLayout Layout { get; set; } = new();
    public List<PlacedBuilding> Buildings { get; set; } = [];
    public List<PlacedProp> Props { get; set; } = [];
    public List<TownZone> Zones { get; set; } = [];
    public TownSpatialSpecification? SpatialSpec { get; set; }
    public string? SpatialSpecJson { get; set; }

    /// <summary>
    /// Creates a TownMapResult with automatic serialization of the spatial spec to JSON.
    /// </summary>
    public static TownMapResult CreateWithSerializedSpec(
        TownLayout layout,
        List<PlacedBuilding> buildings,
        List<PlacedProp> props,
        List<TownZone> zones,
        TownSpatialSpecification? spatialSpec = null)
    {
        var serializedJson = spatialSpec != null
            ? SpatialSpecSerializer.SerializeToJson(spatialSpec)
            : null;

        return new TownMapResult
        {
            Layout = layout,
            Buildings = buildings,
            Props = props,
            Zones = zones,
            SpatialSpec = spatialSpec,
            SpatialSpecJson = serializedJson
        };
    }
}

public sealed class TownLayout
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int[][] Surface { get; set; } = [];
    public int[][]? Liquid { get; set; }
}

public sealed class PlacedBuilding
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string MeshAsset { get; set; } = "";
    public string SizeCategory { get; set; } = "";
    public int[][] Footprint { get; set; } = [];
    public int Floors { get; set; }
    public float Condition { get; set; }
    public TilePlacement Placement { get; set; } = new(0, 0, 0, 0);
}

public sealed class PlacedProp
{
    public string Id { get; set; } = "";
    public string MeshAsset { get; set; } = "";
    public TilePlacement Placement { get; set; } = new(0, 0, 0, 0);
    public float Rotation { get; set; }
    public float Scale { get; set; }
    public bool BlocksWalkability { get; set; }
}

public sealed class TownZone
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public int Biome { get; set; }
    public float RadiationLevel { get; set; }
    public int EnemyDifficultyTier { get; set; }
    public bool IsFastTravelTarget { get; set; }
    public int ChunkStartX { get; set; }
    public int ChunkStartY { get; set; }
    public int ChunkEndX { get; set; }
    public int ChunkEndY { get; set; }
}

public sealed record TilePlacement(
    int ChunkX, int ChunkY,
    int LocalTileX, int LocalTileY);
