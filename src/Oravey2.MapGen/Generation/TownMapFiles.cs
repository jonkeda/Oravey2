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
            JsonSerializer.Serialize(
                new LayoutDto(result.Layout.Width, result.Layout.Height, result.Layout.Surface, result.Layout.Liquid),
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "buildings.json"),
            JsonSerializer.Serialize(result.Buildings.Select(b => new BuildingDto(
                b.Id, b.Name, b.MeshAsset, b.SizeCategory,
                b.Footprint, b.Floors, b.Condition, null,
                PlacementFrom(b.Placement))).ToList(),
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "props.json"),
            JsonSerializer.Serialize(result.Props.Select(p => new PropDto(
                p.Id, p.MeshAsset, PlacementFrom(p.Placement),
                p.Rotation, p.Scale, p.BlocksWalkability, null)).ToList(),
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "zones.json"),
            JsonSerializer.Serialize(result.Zones.Select(z => new ZoneDto(
                z.Id, z.Name, z.Biome, z.RadiationLevel,
                z.EnemyDifficultyTier, z.IsFastTravelTarget,
                z.ChunkStartX, z.ChunkStartY,
                z.ChunkEndX, z.ChunkEndY)).ToList(),
                ContentPackSerializer.WriteOptions));
    }

    public static TownMapResult Load(string townDir)
    {
        var layoutJson = File.ReadAllText(Path.Combine(townDir, "layout.json"));
        var layoutDto = JsonSerializer.Deserialize<LayoutDto>(layoutJson, ContentPackSerializer.ReadOptions);

        var buildingsJson = File.ReadAllText(Path.Combine(townDir, "buildings.json"));
        var buildingDtos = JsonSerializer.Deserialize<List<BuildingDto>>(buildingsJson, ContentPackSerializer.ReadOptions) ?? [];

        var propsJson = File.ReadAllText(Path.Combine(townDir, "props.json"));
        var propDtos = JsonSerializer.Deserialize<List<PropDto>>(propsJson, ContentPackSerializer.ReadOptions) ?? [];

        var zonesJson = File.ReadAllText(Path.Combine(townDir, "zones.json"));
        var zoneDtos = JsonSerializer.Deserialize<List<ZoneDto>>(zonesJson, ContentPackSerializer.ReadOptions) ?? [];

        var layout = new TownLayout(
            layoutDto?.Width ?? 0, layoutDto?.Height ?? 0,
            layoutDto?.Surface ?? [], layoutDto?.Liquid);

        var buildings = buildingDtos.Select(b => new PlacedBuilding(
            b.Id, b.Name, b.MeshAsset, b.Size,
            b.Footprint ?? [],
            b.Floors, b.Condition,
            PlacementTo(b.Placement))).ToList();

        var props = propDtos.Select(p => new PlacedProp(
            p.Id, p.MeshAsset, PlacementTo(p.Placement),
            p.Rotation, p.Scale, p.BlocksWalkability)).ToList();

        var zones = zoneDtos.Select(z => new TownZone(
            z.Id, z.Name, z.Biome, z.RadiationLevel,
            z.EnemyDifficultyTier, z.IsFastTravelTarget,
            z.ChunkStartX, z.ChunkStartY,
            z.ChunkEndX, z.ChunkEndY)).ToList();

        return new TownMapResult(layout, buildings, props, zones);
    }

    private static PlacementDto PlacementFrom(TilePlacement p) =>
        new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);

    private static TilePlacement PlacementTo(PlacementDto? p) =>
        p is null ? new(0, 0, 0, 0) : new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);
}
