namespace Oravey2.MapGen.RegionTemplates;

/// <summary>
/// A region from the Geofabrik index, used to build the picker tree.
/// </summary>
public record GeofabrikRegion
{
    public required string Id { get; init; }
    public string? Parent { get; init; }
    public required string Name { get; init; }
    public string? PbfUrl { get; init; }
    public string[]? Iso3166Alpha2 { get; init; }
    public string[]? Iso3166_2 { get; init; }
    public BoundingBox? Bounds { get; init; }
    public List<GeofabrikRegion> Children { get; } = [];

    /// <summary>
    /// Generate a RegionPreset from this region's metadata and bounding box.
    /// </summary>
    public RegionPreset ToRegionPreset()
    {
        var bbox = Bounds ?? throw new InvalidOperationException(
            $"Region '{Id}' has no bounding box data.");

        return new RegionPreset
        {
            Name = Id,
            DisplayName = Name,
            RegionCode = Iso3166_2?.FirstOrDefault() ?? Iso3166Alpha2?.FirstOrDefault() ?? string.Empty,
            NorthLat = bbox.North,
            SouthLat = bbox.South,
            EastLon = bbox.East,
            WestLon = bbox.West,
            OsmDownloadUrl = PbfUrl ?? string.Empty,
            DefaultCullSettings = new CullSettings()
        };
    }
}

public record BoundingBox(double North, double South, double East, double West);
