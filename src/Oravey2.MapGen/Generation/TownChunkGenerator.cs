using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class TownChunkGenerator
{
    public const int ChunkSize = ChunkData.Size;

    private TownSpatialTransform? _spatialTransform;
    private HashSet<string>? _buildingsApplied;

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

    /// <summary>
    /// Generates chunk using spatial specification (roads, water, buildings from spatial spec).
    /// Each pass builds on the previous without overwriting existing features.
    /// </summary>
    public ChunkResult GenerateWithSpatialSpec(
        TownSpatialTransform spatialTransform,
        CuratedTown town,
        TownEntry townEntry,
        int chunkX, int chunkY,
        RegionTemplate region,
        int seed)
    {
        int chunkSeed = HashCode.Combine(seed, chunkX, chunkY, town.GameName);
        var rng = new Random(chunkSeed);

        _spatialTransform = spatialTransform;
        _buildingsApplied = new();

        var tiles = new TileMapData(ChunkSize, ChunkSize);
        var entities = new List<EntitySpawnInfo>();

        float chunkWorldX = chunkX * ChunkSize;
        float chunkWorldZ = chunkY * ChunkSize;

        // Pass 1: Base terrain (grass/dirt)
        Pass1_BaseTerrain(tiles, chunkWorldX, chunkWorldZ, region, rng);

        // Pass 2: Water bodies from spatial spec
        Pass2_Water(tiles, chunkWorldX, chunkWorldZ, spatialTransform);

        // Pass 3: Road network from spatial spec
        Pass3_Roads(tiles, chunkWorldX, chunkWorldZ, spatialTransform);

        // Pass 4: Building placements from spatial spec
        Pass4_Buildings(tiles, entities, chunkWorldX, chunkWorldZ, spatialTransform);

        return new ChunkResult(chunkX, chunkY, tiles, entities, ChunkMode.Hybrid);
    }

    private static void Pass1_BaseTerrain(
        TileMapData tiles, float chunkWorldX, float chunkWorldZ, RegionTemplate region, Random rng)
    {
        for (int lx = 0; lx < ChunkSize; lx++)
        {
            for (int ly = 0; ly < ChunkSize; ly++)
            {
                float worldX = chunkWorldX + lx;
                float worldZ = chunkWorldZ + ly;

                float elevation = WildernessChunkGenerator.SampleElevation(region, worldX, worldZ);
                byte heightLevel = WildernessChunkGenerator.QuantiseHeight(elevation);
                byte variant = (byte)rng.Next(256);

                tiles.SetTileData(lx, ly, new TileData(
                    SurfaceType.Grass, heightLevel, 0, 0, TileFlags.Walkable, variant));
            }
        }
    }

    private static void Pass2_Water(
        TileMapData tiles, float chunkWorldX, float chunkWorldZ, TownSpatialTransform spatialTransform)
    {
        var waterBodies = spatialTransform.TransformWaterBodies();

        foreach (var water in waterBodies)
        {
            // Rasterize water polygon and apply to tiles in this chunk
            ApplyWaterBodyToChunk(tiles, chunkWorldX, chunkWorldZ, water);
        }
    }

    private static void Pass3_Roads(
        TileMapData tiles, float chunkWorldX, float chunkWorldZ, TownSpatialTransform spatialTransform)
    {
        var tileRoads = spatialTransform.TransformRoadNetwork();

        foreach (var road in tileRoads)
        {
            // Rasterize each road segment and apply to tiles in this chunk
            ApplyRoadSegmentToChunk(tiles, chunkWorldX, chunkWorldZ, road);
        }
    }

    private void Pass4_Buildings(
        TileMapData tiles,
        List<EntitySpawnInfo> entities,
        float chunkWorldX,
        float chunkWorldZ,
        TownSpatialTransform spatialTransform)
    {
        var buildingPlacements = spatialTransform.TransformBuildingPlacements();
        var (gridWidth, gridHeight) = spatialTransform.GetGridDimensions();
        _buildingsApplied ??= new();

        foreach (var (buildingName, placement) in buildingPlacements)
        {
            // Skip if already applied to avoid duplicates
            if (_buildingsApplied.Contains(buildingName))
                continue;

            // Rasterize building footprint using full grid dimensions
            var footprint = BuildingPlacer.RasterizeBuilding(
                placement,
                gridWidth,
                gridHeight);

            int collisionCount = 0;
            bool hasChunkTiles = false;

            // Apply building only to tiles within this chunk
            for (int lx = 0; lx < ChunkSize; lx++)
            {
                for (int ly = 0; ly < ChunkSize; ly++)
                {
                    int worldX = (int)chunkWorldX + lx;
                    int worldZ = (int)chunkWorldZ + ly;

                    // Check if any footprint tile matches this world tile
                    foreach (var footprintTile in footprint)
                    {
                        if (footprintTile.Length < 2) continue;

                        if (footprintTile[0] == worldX && footprintTile[1] == worldZ)
                        {
                            hasChunkTiles = true;
                            var existing = tiles.GetTileData(lx, ly);

                            // Count collision if overwriting non-grass
                            if (existing.Surface != SurfaceType.Grass)
                            {
                                collisionCount++;
                            }

                            // Apply building: keep height, set walkable to false
                            tiles.SetTileData(lx, ly, new TileData(
                                SurfaceType.Concrete, existing.HeightLevel, 0, 0,
                                existing.Flags & ~TileFlags.Walkable, existing.VariantSeed));
                        }
                    }
                }
            }

            if (hasChunkTiles)
            {
                _buildingsApplied.Add(buildingName);
                // Log building placement if needed
                System.Diagnostics.Debug.WriteLine(
                    $"Building {buildingName} at ({placement.CenterX},{placement.CenterZ}): {collisionCount} collisions");
            }
        }
    }

    private static void ApplyWaterBodyToChunk(
        TileMapData tiles, float chunkWorldX, float chunkWorldZ, TileWaterBody water)
    {
        // Simple point-in-polygon rasterization for water
        // For now, rasterize bounding box of water polygon
        if (water.Polygon.Count == 0) return;

        float minX = water.Polygon[0].X;
        float maxX = water.Polygon[0].X;
        float minZ = water.Polygon[0].Y;
        float maxZ = water.Polygon[0].Y;

        foreach (var point in water.Polygon)
        {
            minX = Math.Min(minX, point.X);
            maxX = Math.Max(maxX, point.X);
            minZ = Math.Min(minZ, point.Y);
            maxZ = Math.Max(maxZ, point.Y);
        }

        int startLx = Math.Max(0, (int)(minX - chunkWorldX));
        int endLx = Math.Min(ChunkSize - 1, (int)(maxX - chunkWorldX));
        int startLy = Math.Max(0, (int)(minZ - chunkWorldZ));
        int endLy = Math.Min(ChunkSize - 1, (int)(maxZ - chunkWorldZ));

        for (int lx = startLx; lx <= endLx; lx++)
        {
            for (int ly = startLy; ly <= endLy; ly++)
            {
                var existing = tiles.GetTileData(lx, ly);
                // Mark water by setting high water level (creates water on any ground)
                tiles.SetTileData(lx, ly, new TileData(
                    SurfaceType.Dirt, existing.HeightLevel, (byte)(existing.HeightLevel + 5), 0,
                    TileFlags.None, existing.VariantSeed, LiquidType.Water));
            }
        }
    }

    private static void ApplyRoadSegmentToChunk(
        TileMapData tiles, float chunkWorldX, float chunkWorldZ, TileRoadSegment road)
    {
        float halfWidth = road.WidthTiles / 2f;
        RasteriseSegment(tiles, chunkWorldX, chunkWorldZ, road.From, road.To, halfWidth);
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
