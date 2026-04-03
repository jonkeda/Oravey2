using System.Text.Json;
using Oravey2.Core.World.Serialization;

namespace Oravey2.Core.World.Blueprint;

public sealed record CompilationResult(
    bool Success,
    int ChunksGenerated,
    int BuildingsPlaced,
    int PropsPlaced,
    ValidationError[] Warnings
);

public static class MapCompiler
{
    /// <summary>
    /// Compiles a MapBlueprint into runtime chunk files in the output directory.
    /// </summary>
    public static CompilationResult Compile(MapBlueprint blueprint, string outputDirectory)
    {
        // 1. Validate
        var validation = BlueprintValidator.Validate(blueprint);
        if (!validation.IsValid)
            return new CompilationResult(false, 0, 0, 0, validation.Errors);

        int chunksWide = blueprint.Dimensions.ChunksWide;
        int chunksHigh = blueprint.Dimensions.ChunksHigh;

        // 2. Terrain pass
        var grid = TerrainCompiler.Compile(blueprint);

        // 3. Road pass
        if (blueprint.Roads is { Length: > 0 })
            RoadCompiler.CompileRoads(grid, blueprint.Roads);

        // 4. Water pass
        if (blueprint.Water != null)
            WaterCompiler.CompileWater(grid, blueprint.Water);

        // 5. Structure pass
        BuildingDefinition[] buildings = Array.Empty<BuildingDefinition>();
        PropDefinition[] props = Array.Empty<PropDefinition>();
        if (blueprint.Buildings is { Length: > 0 } || blueprint.Props is { Length: > 0 })
        {
            (buildings, props) = StructureCompiler.CompileStructures(
                grid, blueprint.Buildings, blueprint.Props);
        }

        // 6. Zone pass
        var zones = ZoneCompiler.CompileZones(blueprint.Zones);

        // 7. Split grid into 16×16 chunks and write files
        Directory.CreateDirectory(outputDirectory);
        var chunksDir = Path.Combine(outputDirectory, "chunks");
        Directory.CreateDirectory(chunksDir);

        int chunksGenerated = 0;
        for (int cx = 0; cx < chunksWide; cx++)
        {
            for (int cy = 0; cy < chunksHigh; cy++)
            {
                var map = new TileMapData(ChunkData.Size, ChunkData.Size);
                for (int lx = 0; lx < ChunkData.Size; lx++)
                {
                    for (int ly = 0; ly < ChunkData.Size; ly++)
                    {
                        int gx = cx * ChunkData.Size + lx;
                        int gy = cy * ChunkData.Size + ly;
                        map.SetTileData(lx, ly, grid[gx, gy]);
                    }
                }

                var chunk = new ChunkData(cx, cy, map);
                ChunkSerializer.SaveChunk(chunk, chunksDir);
                chunksGenerated++;
            }
        }

        // 8. Write world.json
        var worldJson = new WorldJson(chunksWide, chunksHigh, 1f,
            new PlayerStartJson(0, 0, 0, 0), null);
        var worldJsonStr = JsonSerializer.Serialize(worldJson, BlueprintLoader.WriteOptions);
        File.WriteAllText(Path.Combine(outputDirectory, "world.json"), worldJsonStr);

        // 9. Write buildings.json if any
        if (buildings.Length > 0)
        {
            var buildingJsons = buildings.Select((b, i) =>
            {
                int chunkX = (blueprint.Buildings![i].TileX) / ChunkData.Size;
                int chunkY = (blueprint.Buildings[i].TileY) / ChunkData.Size;
                return BuildingSerializer.ToBuildingJson(b, chunkX, chunkY);
            });
            BuildingSerializer.SaveBuildings(buildingJsons, outputDirectory);
        }

        // 10. Write props.json if any
        if (props.Length > 0)
        {
            var propJsons = props.Select(BuildingSerializer.ToPropJson);
            BuildingSerializer.SaveProps(propJsons, outputDirectory);
        }

        // 11. Write zones.json if any
        if (zones.Length > 0)
        {
            var zonesJsonStr = JsonSerializer.Serialize(zones, BlueprintLoader.WriteOptions);
            File.WriteAllText(Path.Combine(outputDirectory, "zones.json"), zonesJsonStr);
        }

        return new CompilationResult(true, chunksGenerated, buildings.Length, props.Length,
            Array.Empty<ValidationError>());
    }
}
