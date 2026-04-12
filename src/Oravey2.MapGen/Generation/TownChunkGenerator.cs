using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class TownChunkGenerator
{
    public const int ChunkSize = ChunkData.Size;

    public ChunkResult Generate(
        int chunkX, int chunkY,
        CuratedTown town,
        TownEntry townEntry,
        RegionTemplate region,
        int seed)
    {
        int chunkSeed = HashCode.Combine(seed, chunkX, chunkY, town.GameName);
        var rng = new Random(chunkSeed);

        var tiles = new TileMapData(ChunkSize, ChunkSize);
        var entities = new List<EntitySpawnInfo>();

        float chunkWorldX = chunkX * ChunkSize;
        float chunkWorldZ = chunkY * ChunkSize;

        // First pass: base terrain
        for (int lx = 0; lx < ChunkSize; lx++)
        {
            for (int ly = 0; ly < ChunkSize; ly++)
            {
                float worldX = chunkWorldX + lx;
                float worldZ = chunkWorldZ + ly;

                float elevation = WildernessChunkGenerator.SampleElevation(region, worldX, worldZ);
                byte heightLevel = WildernessChunkGenerator.QuantiseHeight(elevation);
                byte variant = (byte)rng.Next(256);

                // Default to road/concrete for town areas
                tiles.SetTileData(lx, ly, new TileData(
                    SurfaceType.Concrete, heightLevel, 0, 0, TileFlags.Walkable, variant));
            }
        }

        // Second pass: road skeleton from OSM
        ApplyRoadSkeleton(tiles, chunkWorldX, chunkWorldZ, region.Roads);

        // Third pass: buildings
        var budget = BuildingBudget(townEntry.Category);
        PlaceBuildings(tiles, entities, rng, budget, chunkWorldX, chunkWorldZ, region, town);

        return new ChunkResult(chunkX, chunkY, tiles, entities, ChunkMode.Hybrid);
    }

    private static void ApplyRoadSkeleton(
        TileMapData tiles, float chunkWorldX, float chunkWorldZ, List<RoadSegment> roads)
    {
        foreach (var road in roads)
        {
            float halfWidth = road.RoadClass switch
            {
                LinearFeatureType.Motorway => 6f,
                LinearFeatureType.Trunk => 4f,
                LinearFeatureType.Primary => 3f,
                _ => 2f
            };

            for (int i = 0; i < road.Nodes.Length - 1; i++)
            {
                RasteriseSegment(tiles, chunkWorldX, chunkWorldZ,
                    road.Nodes[i], road.Nodes[i + 1], halfWidth);
            }
        }
    }

    private static void RasteriseSegment(
        TileMapData tiles, float chunkX, float chunkZ,
        Vector2 a, Vector2 b, float halfWidth)
    {
        float minX = Math.Min(a.X, b.X) - halfWidth;
        float maxX = Math.Max(a.X, b.X) + halfWidth;
        float minZ = Math.Min(a.Y, b.Y) - halfWidth;
        float maxZ = Math.Max(a.Y, b.Y) + halfWidth;

        int startLx = Math.Max(0, (int)(minX - chunkX));
        int endLx = Math.Min(ChunkSize - 1, (int)(maxX - chunkX));
        int startLy = Math.Max(0, (int)(minZ - chunkZ));
        int endLy = Math.Min(ChunkSize - 1, (int)(maxZ - chunkZ));

        var dir = b - a;
        float len = dir.Length();
        if (len < 0.001f) return;
        dir /= len;

        for (int lx = startLx; lx <= endLx; lx++)
        {
            for (int ly = startLy; ly <= endLy; ly++)
            {
                var p = new Vector2(chunkX + lx, chunkZ + ly);
                var ap = p - a;
                float proj = Math.Clamp(Vector2.Dot(ap, dir), 0, len);
                var closest = a + dir * proj;
                float dist = Vector2.Distance(p, closest);

                if (dist <= halfWidth)
                {
                    var existing = tiles.GetTileData(lx, ly);
                    tiles.SetTileData(lx, ly, new TileData(
                        SurfaceType.Asphalt, existing.HeightLevel, 0, 0,
                        TileFlags.Walkable, existing.VariantSeed));
                }
            }
        }
    }

    internal static (int Min, int Max) BuildingBudget(TownCategory category) => category switch
    {
        TownCategory.Hamlet => (2, 4),
        TownCategory.Village => (4, 8),
        TownCategory.Town => (8, 16),
        TownCategory.City => (12, 24),
        TownCategory.Metropolis => (16, 32),
        _ => (4, 8)
    };

    private static void PlaceBuildings(
        TileMapData tiles,
        List<EntitySpawnInfo> entities,
        Random rng,
        (int Min, int Max) budget,
        float chunkWorldX,
        float chunkWorldZ,
        RegionTemplate region,
        CuratedTown town)
    {
        int count = rng.Next(budget.Min, budget.Max + 1);
        const float minSpacing = 8f;
        var placed = new List<Vector2>();

        for (int attempt = 0; attempt < count * 10 && placed.Count < count; attempt++)
        {
            int lx = rng.Next(1, ChunkSize - 3);
            int ly = rng.Next(1, ChunkSize - 3);
            var pos = new Vector2(chunkWorldX + lx, chunkWorldZ + ly);

            // Check spacing
            if (placed.Any(p => Vector2.Distance(p, pos) < minSpacing))
                continue;

            // Check tile is walkable and not already a structure
            var existing = tiles.GetTileData(lx, ly);
            if (existing.StructureId != 0 || existing.Surface == SurfaceType.Asphalt)
                continue;

            // Place 2x2 building footprint
            int structureId = HashCode.Combine(town.GameName, lx, ly);
            if (structureId == 0) structureId = 1;

            bool canPlace = true;
            for (int dx = 0; dx < 2 && canPlace; dx++)
                for (int dy = 0; dy < 2 && canPlace; dy++)
                {
                    int tx = lx + dx, ty = ly + dy;
                    if (tx >= ChunkSize || ty >= ChunkSize) { canPlace = false; break; }
                    var t = tiles.GetTileData(tx, ty);
                    if (t.StructureId != 0 || t.Surface == SurfaceType.Asphalt) canPlace = false;
                }

            if (!canPlace) continue;

            for (int dx = 0; dx < 2; dx++)
                for (int dy = 0; dy < 2; dy++)
                {
                    int tx = lx + dx, ty = ly + dy;
                    var t = tiles.GetTileData(tx, ty);
                    tiles.SetTileData(tx, ty, new TileData(
                        SurfaceType.Concrete, t.HeightLevel, 0, structureId,
                        t.Flags & ~TileFlags.Walkable, t.VariantSeed));
                }

            placed.Add(pos);
            entities.Add(new EntitySpawnInfo(
                PrefabId: "building_ruin",
                LocalX: lx + 1f,
                LocalZ: ly + 1f,
                RotationY: rng.Next(4) * 90f));
        }
    }
}
