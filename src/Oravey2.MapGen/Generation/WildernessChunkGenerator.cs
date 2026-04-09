using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class WildernessChunkGenerator
{
    public const int ChunkSize = ChunkData.Size;
    public const float TileSize = 1f; // 1 metre per tile

    public ChunkResult Generate(
        int chunkX, int chunkY,
        int regionSeed,
        RegionTemplate region)
    {
        int seed = HashCode.Combine(regionSeed, chunkX, chunkY);
        var rng = new Random(seed);

        var tiles = new TileMapData(ChunkSize, ChunkSize);
        var entities = new List<EntitySpawnInfo>();

        // World position of chunk origin in metres
        float chunkWorldX = chunkX * ChunkSize * TileSize;
        float chunkWorldZ = chunkY * ChunkSize * TileSize;

        for (int lx = 0; lx < ChunkSize; lx++)
        {
            for (int ly = 0; ly < ChunkSize; ly++)
            {
                float worldX = chunkWorldX + lx * TileSize;
                float worldZ = chunkWorldZ + ly * TileSize;

                float elevation = SampleElevation(region, worldX, worldZ);
                byte heightLevel = QuantiseHeight(elevation);
                var landUse = SampleLandUse(region, worldX, worldZ);
                var (surface, flags) = MapLandUseToTile(landUse);

                byte variant = (byte)(rng.Next(256));

                // Decay detail: rubble and cracks in urban areas
                if (surface == SurfaceType.Concrete && rng.NextDouble() < 0.15)
                {
                    surface = SurfaceType.Rock; // rubble
                    flags |= TileFlags.Searchable;
                }

                tiles.SetTileData(lx, ly, new TileData(
                    surface, heightLevel, 0, 0, flags, variant));

                // Tree spawns in forested areas
                if (flags.HasFlag(TileFlags.Forested) && rng.NextDouble() < 0.3)
                {
                    entities.Add(new EntitySpawnInfo(
                        PrefabId: "tree_deciduous",
                        LocalX: lx + (float)(rng.NextDouble() * 0.6 - 0.3),
                        LocalZ: ly + (float)(rng.NextDouble() * 0.6 - 0.3),
                        RotationY: (float)(rng.NextDouble() * 360)));
                }
            }
        }

        return new ChunkResult(chunkX, chunkY, tiles, entities, ChunkMode.Heightmap);
    }

    internal static float SampleElevation(RegionTemplate region, float worldX, float worldZ)
    {
        double cellSize = region.GridCellSizeMetres;
        if (cellSize <= 0) return 0;

        int rows = region.ElevationGrid.GetLength(0);
        int cols = region.ElevationGrid.GetLength(1);

        double col = worldX / cellSize;
        double row = worldZ / cellSize;

        int c = Math.Clamp((int)col, 0, cols - 1);
        int r = Math.Clamp((int)row, 0, rows - 1);

        return region.ElevationGrid[r, c];
    }

    internal static LandUseType? SampleLandUse(RegionTemplate region, float worldX, float worldZ)
    {
        var point = new Vector2(worldX, worldZ);
        foreach (var zone in region.LandUseZones)
        {
            if (ContinentGenerator.PointInPolygon(point, zone.Polygon))
                return zone.Type;
        }
        return null;
    }

    internal static (SurfaceType Surface, TileFlags Flags) MapLandUseToTile(LandUseType? landUse) => landUse switch
    {
        LandUseType.Farmland => (SurfaceType.Grass, TileFlags.Walkable),
        LandUseType.Forest => (SurfaceType.Grass, TileFlags.Walkable | TileFlags.Forested),
        LandUseType.Residential => (SurfaceType.Concrete, TileFlags.Walkable),
        LandUseType.Industrial => (SurfaceType.Concrete, TileFlags.Walkable),
        LandUseType.Commercial => (SurfaceType.Concrete, TileFlags.Walkable),
        LandUseType.Meadow => (SurfaceType.Grass, TileFlags.Walkable),
        LandUseType.Orchard => (SurfaceType.Grass, TileFlags.Walkable | TileFlags.Forested),
        LandUseType.Cemetery => (SurfaceType.Dirt, TileFlags.Walkable),
        LandUseType.Military => (SurfaceType.Concrete, TileFlags.Walkable),
        _ => (SurfaceType.Dirt, TileFlags.Walkable)
    };

    internal static byte QuantiseHeight(float elevationMetres)
    {
        // Map real elevation to byte: 0m → 0, each step = 0.25m world units
        // Clamp to byte range
        float steps = elevationMetres / HeightHelper.HeightStep;
        return (byte)Math.Clamp((int)steps, 0, 255);
    }
}

public sealed record ChunkResult(
    int ChunkX,
    int ChunkY,
    TileMapData Tiles,
    List<EntitySpawnInfo> Entities,
    ChunkMode Mode);
