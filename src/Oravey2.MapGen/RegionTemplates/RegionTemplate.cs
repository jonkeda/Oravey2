namespace Oravey2.MapGen.RegionTemplates;

public class RegionTemplate
{
    public required string Name { get; init; }
    public required float[,] ElevationGrid { get; init; }
    public required double GridOriginLat { get; init; }
    public required double GridOriginLon { get; init; }
    public required double GridCellSizeMetres { get; init; }
    public List<TownEntry> Towns { get; init; } = [];
    public List<RoadSegment> Roads { get; init; } = [];
    public List<WaterBody> WaterBodies { get; init; } = [];
    public List<RailwaySegment> Railways { get; init; } = [];
    public List<LandUseZone> LandUseZones { get; init; } = [];
}
