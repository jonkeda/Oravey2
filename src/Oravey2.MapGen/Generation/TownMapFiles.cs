using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// JSON serialization for town map output files: layout.json, buildings.json,
/// props.json, and zones.json — matching the existing portland map format.
/// </summary>
public static class TownMapFiles
{
    public static void Save(TownMapResult result, string townDir)
    {
        Directory.CreateDirectory(townDir);

        File.WriteAllText(
            Path.Combine(townDir, "layout.json"),
            JsonSerializer.Serialize(result.Layout, ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "buildings.json"),
            JsonSerializer.Serialize(result.Buildings, ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "props.json"),
            JsonSerializer.Serialize(result.Props, ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "zones.json"),
            JsonSerializer.Serialize(result.Zones, ContentPackSerializer.WriteOptions));
    }

    public static TownMapResult Load(string townDir)
    {
        var layoutJson = File.ReadAllText(Path.Combine(townDir, "layout.json"));
        var layout = JsonSerializer.Deserialize<LayoutDto>(layoutJson, ContentPackSerializer.ReadOptions) ?? new();

        var buildingsJson = File.ReadAllText(Path.Combine(townDir, "buildings.json"));
        var buildings = JsonSerializer.Deserialize<List<BuildingDto>>(buildingsJson, ContentPackSerializer.ReadOptions) ?? [];

        var propsJson = File.ReadAllText(Path.Combine(townDir, "props.json"));
        var props = JsonSerializer.Deserialize<List<PropDto>>(propsJson, ContentPackSerializer.ReadOptions) ?? [];

        var zonesJson = File.ReadAllText(Path.Combine(townDir, "zones.json"));
        var zones = JsonSerializer.Deserialize<List<ZoneDto>>(zonesJson, ContentPackSerializer.ReadOptions) ?? [];

        return new TownMapResult { Layout = layout, Buildings = buildings, Props = props, Zones = zones };
    }
}
