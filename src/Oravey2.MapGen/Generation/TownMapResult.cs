using Oravey2.Contracts.ContentPack;
using Oravey2.Contracts.Spatial;

namespace Oravey2.MapGen.Generation;

public sealed class TownMapResult
{
    public LayoutDto Layout { get; set; } = new();
    public List<BuildingDto> Buildings { get; set; } = [];
    public List<PropDto> Props { get; set; } = [];
    public List<ZoneDto> Zones { get; set; } = [];
    public TownSpatialSpecification? SpatialSpec { get; set; }
    public string? SpatialSpecJson { get; set; }

    public static TownMapResult CreateWithSerializedSpec(
        LayoutDto layout,
        List<BuildingDto> buildings,
        List<PropDto> props,
        List<ZoneDto> zones,
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
