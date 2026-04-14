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
                new LayoutDto { Width = result.Layout.Width, Height = result.Layout.Height, Surface = result.Layout.Surface, Liquid = result.Layout.Liquid },
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "buildings.json"),
            JsonSerializer.Serialize(result.Buildings.Select(b => new BuildingDto
            {
                Id = b.Id, Name = b.Name, MeshAsset = b.MeshAsset, Size = b.SizeCategory,
                Footprint = b.Footprint, Floors = b.Floors, Condition = b.Condition, InteriorChunkId = null,
                Placement = PlacementFrom(b.Placement),
            }).ToList(),
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "props.json"),
            JsonSerializer.Serialize(result.Props.Select(p => new PropDto
            {
                Id = p.Id, MeshAsset = p.MeshAsset, Placement = PlacementFrom(p.Placement),
                Rotation = p.Rotation, Scale = p.Scale, BlocksWalkability = p.BlocksWalkability, Footprint = null,
            }).ToList(),
                ContentPackSerializer.WriteOptions));

        File.WriteAllText(
            Path.Combine(townDir, "zones.json"),
            JsonSerializer.Serialize(result.Zones.Select(z => new ZoneDto
            {
                Id = z.Id, Name = z.Name, Biome = z.Biome, RadiationLevel = z.RadiationLevel,
                EnemyDifficultyTier = z.EnemyDifficultyTier, IsFastTravelTarget = z.IsFastTravelTarget,
                ChunkStartX = z.ChunkStartX, ChunkStartY = z.ChunkStartY,
                ChunkEndX = z.ChunkEndX, ChunkEndY = z.ChunkEndY,
            }).ToList(),
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

        var layout = new TownLayout
        {
            Width = layoutDto?.Width ?? 0, Height = layoutDto?.Height ?? 0,
            Surface = layoutDto?.Surface ?? [], Liquid = layoutDto?.Liquid,
        };

        var buildings = buildingDtos.Select(b => new PlacedBuilding
        {
            Id = b.Id, Name = b.Name, MeshAsset = b.MeshAsset, SizeCategory = b.Size,
            Footprint = b.Footprint ?? [],
            Floors = b.Floors, Condition = b.Condition,
            Placement = PlacementTo(b.Placement),
        }).ToList();

        var props = propDtos.Select(p => new PlacedProp
        {
            Id = p.Id, MeshAsset = p.MeshAsset, Placement = PlacementTo(p.Placement),
            Rotation = p.Rotation, Scale = p.Scale, BlocksWalkability = p.BlocksWalkability,
        }).ToList();

        var zones = zoneDtos.Select(z => new TownZone
        {
            Id = z.Id, Name = z.Name, Biome = z.Biome, RadiationLevel = z.RadiationLevel,
            EnemyDifficultyTier = z.EnemyDifficultyTier, IsFastTravelTarget = z.IsFastTravelTarget,
            ChunkStartX = z.ChunkStartX, ChunkStartY = z.ChunkStartY,
            ChunkEndX = z.ChunkEndX, ChunkEndY = z.ChunkEndY,
        }).ToList();

        return new TownMapResult { Layout = layout, Buildings = buildings, Props = props, Zones = zones };
    }

    private static PlacementDto PlacementFrom(TilePlacement p) =>
        new() { ChunkX = p.ChunkX, ChunkY = p.ChunkY, LocalTileX = p.LocalTileX, LocalTileY = p.LocalTileY };

    private static TilePlacement PlacementTo(PlacementDto? p) =>
        p is null ? new(0, 0, 0, 0) : new(p.ChunkX, p.ChunkY, p.LocalTileX, p.LocalTileY);
}
