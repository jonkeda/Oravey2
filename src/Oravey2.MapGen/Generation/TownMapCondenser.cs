using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Generates compact game-scale tile maps for a designed town by condensing
/// real-world OSM geometry into a playable grid.
/// Supports both procedural generation and spatial specification-based generation.
/// </summary>
public sealed class TownMapCondenser
{
    private const int TilesPerChunk = 16;
    private readonly TownDesign? _townDesign;

    private static readonly string[] PropAssets =
    [
        "meshes/barrel.glb",
        "meshes/crate.glb",
        "meshes/vehicle_wreck.glb",
        "meshes/trash_pile.glb",
        "meshes/sandbags.glb",
    ];

    public TownMapCondenser(TownDesign? townDesign = null)
    {
        _townDesign = townDesign;
    }

    /// <summary>Backward-compatible overload using a fixed seed and default params.</summary>
    public TownMapResult Condense(
        CuratedTown town,
        TownDesign design,
        RegionTemplate region,
        int seed)
        => Condense(town, design, region, new MapGenerationParams { Seed = seed });

    public TownMapResult Condense(
        CuratedTown town,
        TownDesign design,
        RegionTemplate region,
        MapGenerationParams parms)
    {
        var rng = new Random(parms.Seed ?? Random.Shared.Next());

        // Step 1: Determine grid size from layout style + key locations
        var (width, height) = ComputeGridSize(design, parms);

        // Step 2: Build surface grid
        var surface = BuildSurface(width, height, rng);

        // Step 3: Snap roads onto grid
        var roadTiles = SnapRoads(width, height, design.LayoutStyle, rng);
        ApplyRoadTiles(surface, roadTiles);

        // Step 4: Place landmark at town centre
        var buildings = new List<PlacedBuilding>();
        var centreX = width / 2;
        var centreY = height / 2;
        var occupied = new HashSet<(int, int)>();

        var landmark = PlaceLandmark(design.Landmarks[0], centreX, centreY, buildings.Count, occupied);
        buildings.Add(landmark);

        // Place additional landmarks distributed around centre
        for (var li = 1; li < design.Landmarks.Count; li++)
        {
            var angle = li * 2.0 * Math.PI / design.Landmarks.Count;
            var offsetX = centreX + (int)(centreX * 0.3 * Math.Cos(angle));
            var offsetY = centreY + (int)(centreY * 0.3 * Math.Sin(angle));
            offsetX = Math.Clamp(offsetX, 2, width - 4);
            offsetY = Math.Clamp(offsetY, 2, height - 4);
            var extra = PlaceLandmark(design.Landmarks[li], offsetX, offsetY, buildings.Count, occupied);
            buildings.Add(extra);
        }

        // Step 5: Place key locations along main roads
        PlaceKeyLocations(design.KeyLocations, roadTiles, width, height, rng, buildings, occupied);

        // Step 6: Fill with generic buildings
        FillGenericBuildings(width, height, rng, buildings, occupied, parms.BuildingFillPercent);

        // Step 7: Place props
        var props = PlaceProps(width, height, rng, occupied, parms.PropDensityPercent, parms.MaxProps);

        // Step 8: Define zones from hazards
        var zones = DefineZones(width, height, design, (int)town.Destruction);

        // Step 9: Build liquid overlay from hazard zones
        var liquid = BuildLiquidLayer(width, height, design.Hazards, zones);

        var layout = new TownLayout { Width = width, Height = height, Surface = surface, Liquid = liquid };
        return new TownMapResult { Layout = layout, Buildings = buildings, Props = props, Zones = zones };
    }

    internal static (int Width, int Height) ComputeGridSize(TownDesign design, MapGenerationParams? parms = null)
    {
        // Explicit grid size override
        if (parms is not null && parms.GridSize != GridSizeMode.Auto)
        {
            var overrideDim = parms.GridSize switch
            {
                GridSizeMode.Small_16 => 16,
                GridSizeMode.Medium_32 => 32,
                GridSizeMode.Large_48 => 48,
                GridSizeMode.Custom => Math.Clamp(parms.CustomGridDimension, 16, 64),
                _ => 0,
            };
            if (overrideDim > 0)
            {
                var overrideChunks = (overrideDim + TilesPerChunk - 1) / TilesPerChunk;
                overrideDim = overrideChunks * TilesPerChunk;
                return (overrideDim, overrideDim);
            }
        }

        // Auto: base size from location count, clamped to reasonable range
        var locationCount = design.KeyLocations.Count + design.Landmarks.Count;
        var baseDim = Math.Max(16, locationCount * 4);
        baseDim = Math.Min(baseDim, 48);

        // Round up to chunk boundaries
        var chunks = (baseDim + TilesPerChunk - 1) / TilesPerChunk;
        var dim = chunks * TilesPerChunk;
        return (dim, dim);
    }

    internal static int[][] BuildSurface(int width, int height, Random rng)
    {
        var surface = new int[height][];
        for (var y = 0; y < height; y++)
        {
            surface[y] = new int[width];
            for (var x = 0; x < width; x++)
                surface[y][x] = rng.Next(2) == 0
                    ? (int)SurfaceType.Dirt
                    : (int)SurfaceType.Grass;
        }
        return surface;
    }

    internal static List<(int X, int Y)> SnapRoads(int width, int height, string layoutStyle, Random rng)
    {
        var roadTiles = new List<(int X, int Y)>();

        switch (layoutStyle.ToLowerInvariant())
        {
            case "grid":
                // Cross-hatch every 4 tiles
                for (var x = 0; x < width; x++)
                    for (var y = 0; y < height; y++)
                        if (x % 4 == 0 || y % 4 == 0)
                            roadTiles.Add((x, y));
                break;

            case "radial":
                // Spokes from centre
                var cx = width / 2;
                var cy = height / 2;
                for (var spoke = 0; spoke < 6; spoke++)
                {
                    var angle = spoke * Math.PI / 3;
                    for (var r = 0; r < Math.Max(width, height) / 2; r++)
                    {
                        var x = cx + (int)(r * Math.Cos(angle));
                        var y = cy + (int)(r * Math.Sin(angle));
                        if (x >= 0 && x < width && y >= 0 && y < height)
                            roadTiles.Add((x, y));
                    }
                }
                break;

            case "linear":
                // One main road through the middle
                for (var x = 0; x < width; x++)
                    roadTiles.Add((x, height / 2));
                // A perpendicular cross
                for (var y = 0; y < height; y++)
                    roadTiles.Add((width / 2, y));
                break;

            default: // organic, clustered, compound
                // Winding road through town with branches
                var mainY = height / 2;
                for (var x = 0; x < width; x++)
                {
                    mainY += rng.Next(-1, 2);
                    mainY = Math.Clamp(mainY, 1, height - 2);
                    roadTiles.Add((x, mainY));
                    roadTiles.Add((x, mainY + 1));
                }
                // A vertical branch
                var branchX = width / 3;
                for (var y = 0; y < height; y++)
                    roadTiles.Add((branchX, y));
                break;
        }

        return roadTiles;
    }

    private static void ApplyRoadTiles(int[][] surface, List<(int X, int Y)> roadTiles)
    {
        foreach (var (x, y) in roadTiles)
        {
            if (y >= 0 && y < surface.Length && x >= 0 && x < surface[y].Length)
                surface[y][x] = (int)SurfaceType.Concrete;
        }
    }

    internal static PlacedBuilding PlaceLandmark(
        LandmarkBuilding landmark,
        int centreX, int centreY,
        int index,
        HashSet<(int, int)> occupied)
    {
        var (w, h, floors) = SizeFromCategory(landmark.SizeCategory);
        var tileX = centreX - w / 2;
        var tileY = centreY - h / 2;

        var footprint = BuildFootprintArray(tileX, tileY, w, h);
        MarkOccupied(occupied, tileX, tileY, w, h);

        return new PlacedBuilding
        {
            Id = $"building_{index}",
            Name = landmark.Name,
            MeshAsset = $"meshes/{landmark.Name.ToLowerInvariant().Replace(' ', '_')}.glb",
            SizeCategory = landmark.SizeCategory,
            Footprint = footprint,
            Floors = floors,
            Condition = 0.6f,
            Placement = new TilePlacement(tileX / TilesPerChunk, tileY / TilesPerChunk,
                                          tileX % TilesPerChunk, tileY % TilesPerChunk),
        };
    }

    internal static void PlaceKeyLocations(
        List<KeyLocation> locations,
        List<(int X, int Y)> roadTiles,
        int width, int height,
        Random rng,
        List<PlacedBuilding> buildings,
        HashSet<(int, int)> occupied)
    {
        // Place each key location near a road tile, offset by 1–2 tiles
        var roadSet = new HashSet<(int, int)>(roadTiles);
        var candidates = roadTiles
            .Select(r => (r.X + 2, r.Y + 1))
            .Where(p => p.Item1 >= 0 && p.Item1 < width && p.Item2 >= 0 && p.Item2 < height)
            .Distinct()
            .ToList();

        foreach (var loc in locations)
        {
            var (w, h, floors) = SizeFromCategory(loc.SizeCategory);

            // Find a candidate that doesn't overlap occupied tiles
            var placed = false;
            for (var attempt = 0; attempt < candidates.Count; attempt++)
            {
                var idx = rng.Next(candidates.Count);
                var (cx, cy) = candidates[idx];

                if (cx + w > width || cy + h > height) continue;
                if (IsOverlapping(occupied, cx, cy, w, h)) continue;

                var footprint = BuildFootprintArray(cx, cy, w, h);
                MarkOccupied(occupied, cx, cy, w, h);

                buildings.Add(new PlacedBuilding
                {
                    Id = $"building_{buildings.Count}",
                    Name = loc.Name,
                    MeshAsset = $"meshes/{loc.Name.ToLowerInvariant().Replace(' ', '_')}.glb",
                    SizeCategory = loc.SizeCategory,
                    Footprint = footprint,
                    Floors = floors,
                    Condition = rng.NextSingle() * 0.4f + 0.4f,
                    Placement = new TilePlacement(cx / TilesPerChunk, cy / TilesPerChunk,
                                                  cx % TilesPerChunk, cy % TilesPerChunk),
                });

                candidates.RemoveAt(idx);
                placed = true;
                break;
            }

            // Fallback: place at first available spot
            if (!placed)
            {
                for (var y = 1; y < height - h; y++)
                {
                    for (var x = 1; x < width - w; x++)
                    {
                        if (IsOverlapping(occupied, x, y, w, h)) continue;

                        var footprint = BuildFootprintArray(x, y, w, h);
                        MarkOccupied(occupied, x, y, w, h);

                        buildings.Add(new PlacedBuilding
                        {
                            Id = $"building_{buildings.Count}",
                            Name = loc.Name,
                            MeshAsset = $"meshes/{loc.Name.ToLowerInvariant().Replace(' ', '_')}.glb",
                            SizeCategory = loc.SizeCategory,
                            Footprint = footprint,
                            Floors = floors,
                            Condition = rng.NextSingle() * 0.4f + 0.4f,
                            Placement = new TilePlacement(x / TilesPerChunk, y / TilesPerChunk,
                                                          x % TilesPerChunk, y % TilesPerChunk),
                        });
                        placed = true;
                        break;
                    }
                    if (placed) break;
                }
            }
        }
    }

    private static void FillGenericBuildings(
        int width, int height, Random rng,
        List<PlacedBuilding> buildings,
        HashSet<(int, int)> occupied,
        int buildingFillPercent = 40)
    {
        // Scale generic count by fill percentage (0–100)
        var maxGeneric = Math.Max(1, (int)(6 * buildingFillPercent / 100.0));
        var genericCount = rng.Next(Math.Max(1, maxGeneric / 2), maxGeneric + 1);
        for (var i = 0; i < genericCount; i++)
        {
            var w = rng.Next(1, 3);
            var h = rng.Next(1, 3);
            for (var attempt = 0; attempt < 50; attempt++)
            {
                var x = rng.Next(1, width - w);
                var y = rng.Next(1, height - h);
                if (IsOverlapping(occupied, x, y, w, h)) continue;

                var footprint = BuildFootprintArray(x, y, w, h);
                MarkOccupied(occupied, x, y, w, h);

                buildings.Add(new PlacedBuilding
                {
                    Id = $"building_{buildings.Count}",
                    Name = $"Ruin {buildings.Count}",
                    MeshAsset = "meshes/generic_ruin.glb",
                    SizeCategory = "small",
                    Footprint = footprint,
                    Floors = 1,
                    Condition = rng.NextSingle() * 0.3f + 0.2f,
                    Placement = new TilePlacement(x / TilesPerChunk, y / TilesPerChunk,
                                                  x % TilesPerChunk, y % TilesPerChunk),
                });
                break;
            }
        }
    }

    internal static List<PlacedProp> PlaceProps(
        int width, int height, Random rng, HashSet<(int, int)> occupied,
        int propDensityPercent = 70, int maxProps = 30)
    {
        var props = new List<PlacedProp>();
        var effectiveMax = Math.Max(0, (int)(maxProps * propDensityPercent / 100.0));
        if (effectiveMax < 1) return props;
        var propCount = Math.Min(rng.Next(1, effectiveMax + 1), effectiveMax);

        for (var i = 0; i < propCount; i++)
        {
            for (var attempt = 0; attempt < 20; attempt++)
            {
                var x = rng.Next(0, width);
                var y = rng.Next(0, height);
                if (occupied.Contains((x, y))) continue;

                occupied.Add((x, y));
                props.Add(new PlacedProp
                {
                    Id = $"prop_{i}",
                    MeshAsset = PropAssets[rng.Next(PropAssets.Length)],
                    Placement = new TilePlacement(x / TilesPerChunk, y / TilesPerChunk,
                                                  x % TilesPerChunk, y % TilesPerChunk),
                    Rotation = rng.NextSingle() * 360f,
                    Scale = 0.8f + rng.NextSingle() * 0.4f,
                    BlocksWalkability = rng.Next(3) == 0,
                });
                break;
            }
        }

        return props;
    }

    internal static List<TownZone> DefineZones(int width, int height, TownDesign design, int threatLevel)
    {
        var zones = new List<TownZone>();

        // Main town zone (always present)
        var chunksWide = width / TilesPerChunk;
        var chunksHigh = height / TilesPerChunk;

        zones.Add(new TownZone
        {
            Id = "zone_main",
            Name = design.TownName,
            Biome = 0,
            RadiationLevel = threatLevel > 5 ? 0.2f : 0f,
            EnemyDifficultyTier = Math.Clamp(threatLevel / 3, 1, 5),
            IsFastTravelTarget = true,
            ChunkStartX = 0, ChunkStartY = 0, ChunkEndX = chunksWide - 1, ChunkEndY = chunksHigh - 1,
        });

        // Add hazard zones
        for (var i = 0; i < design.Hazards.Count; i++)
        {
            var hazard = design.Hazards[i];
            // Place hazard zones at edges based on location hint
            var (sx, sy, ex, ey) = HazardBounds(hazard.LocationHint, chunksWide, chunksHigh);
            zones.Add(new TownZone
            {
                Id = $"zone_hazard_{i}",
                Name = $"{hazard.Type} zone",
                Biome = 1,
                RadiationLevel = hazard.Type.Contains("radiation", StringComparison.OrdinalIgnoreCase) ? 0.5f : 0.1f,
                EnemyDifficultyTier = Math.Clamp(threatLevel / 2, 1, 5),
                IsFastTravelTarget = false,
                ChunkStartX = sx, ChunkStartY = sy, ChunkEndX = ex, ChunkEndY = ey,
            });
        }

        return zones;
    }

    internal static (int StartX, int StartY, int EndX, int EndY) HazardBounds(
        string locationHint, int chunksWide, int chunksHigh)
    {
        var hint = locationHint.ToLowerInvariant();
        if (hint.Contains("north"))
            return (0, 0, chunksWide - 1, 0);
        if (hint.Contains("south"))
            return (0, chunksHigh - 1, chunksWide - 1, chunksHigh - 1);
        if (hint.Contains("east"))
            return (chunksWide - 1, 0, chunksWide - 1, chunksHigh - 1);
        if (hint.Contains("west"))
            return (0, 0, 0, chunksHigh - 1);

        // Default: covers entire map
        return (0, 0, chunksWide - 1, chunksHigh - 1);
    }

    /// <summary>
    /// Builds a liquid layer grid from hazard types and their zone bounds.
    /// Returns null if no hazards produce liquids.
    /// </summary>
    internal static int[][]? BuildLiquidLayer(
        int width, int height,
        List<EnvironmentalHazard> hazards,
        List<TownZone> zones)
    {
        // Map hazard types to LiquidType values
        int[][]? liquid = null;

        for (int i = 0; i < hazards.Count; i++)
        {
            var liquidType = HazardToLiquid(hazards[i].Type);
            if (liquidType == 0) continue; // no liquid for this hazard

            // Find the matching zone (hazard zones start at index 1)
            var zoneIdx = i + 1; // zone[0] is main zone
            if (zoneIdx >= zones.Count) continue;
            var zone = zones[zoneIdx];

            liquid ??= CreateEmptyGrid(width, height);

            // Fill the zone bounds with liquid type
            for (int cy = zone.ChunkStartY; cy <= zone.ChunkEndY; cy++)
            for (int cx = zone.ChunkStartX; cx <= zone.ChunkEndX; cx++)
            for (int ly = 0; ly < TilesPerChunk; ly++)
            for (int lx = 0; lx < TilesPerChunk; lx++)
            {
                int gx = cx * TilesPerChunk + lx;
                int gy = cy * TilesPerChunk + ly;
                if (gx < width && gy < height)
                    liquid[gy][gx] = liquidType;
            }
        }

        return liquid;
    }

    private static int HazardToLiquid(string hazardType)
    {
        var t = hazardType.ToLowerInvariant();
        if (t.Contains("flood") || t.Contains("water") || t.Contains("tidal") || t.Contains("storm surge"))
            return (int)LiquidType.Water;
        if (t.Contains("toxic") || t.Contains("chemical"))
            return (int)LiquidType.Toxic;
        if (t.Contains("acid"))
            return (int)LiquidType.Acid;
        if (t.Contains("sewage"))
            return (int)LiquidType.Sewage;
        if (t.Contains("oil") || t.Contains("petroleum"))
            return (int)LiquidType.Oil;
        return 0; // no liquid
    }

    private static int[][] CreateEmptyGrid(int width, int height)
    {
        var grid = new int[height][];
        for (int y = 0; y < height; y++)
            grid[y] = new int[width];
        return grid;
    }

    private static (int Width, int Height, int Floors) SizeFromCategory(string sizeCategory) =>
        sizeCategory.ToLowerInvariant() switch
        {
            "small" => (1, 1, 1),
            "large" => (3, 3, 3),
            _ => (2, 2, 2), // medium default
        };

    private static int[][] BuildFootprintArray(int tileX, int tileY, int w, int h)
    {
        var result = new int[w * h][];
        var idx = 0;
        for (var dx = 0; dx < w; dx++)
            for (var dy = 0; dy < h; dy++)
                result[idx++] = [tileX + dx, tileY + dy];
        return result;
    }

    private static void MarkOccupied(HashSet<(int, int)> occupied, int x, int y, int w, int h)
    {
        for (var dx = 0; dx < w; dx++)
            for (var dy = 0; dy < h; dy++)
                occupied.Add((x + dx, y + dy));
    }

    private static bool IsOverlapping(HashSet<(int, int)> occupied, int x, int y, int w, int h)
    {
        for (var dx = 0; dx < w; dx++)
            for (var dy = 0; dy < h; dy++)
                if (occupied.Contains((x + dx, y + dy)))
                    return true;
        return false;
    }

    /// <summary>
    /// Condense pre-generated chunks into a final surface map using spatial specification.
    /// Handles variable-sized maps based on spatial spec dimensions.
    /// Falls back to procedural generation if spatial spec is incomplete.
    /// </summary>
    public TownMapResult CondenseWithSpatialSpec(
        TownChunk[][] chunks,
        TownSpatialTransform spatial,
        CancellationToken cancellationToken = default)
    {
        var (gridWidth, gridHeight) = spatial.GetGridDimensions();
        
        // Allocate surface array based on actual grid dimensions
        var surface = new int[gridHeight][];
        for (var y = 0; y < gridHeight; y++)
        {
            surface[y] = new int[gridWidth];
            // Initialize with procedural fallback (grass/dirt)
            for (var x = 0; x < gridWidth; x++)
                surface[y][x] = (int)SurfaceType.Grass;
        }

        // Tile chunks into the surface
        var chunksWide = (gridWidth + TilesPerChunk - 1) / TilesPerChunk;
        var chunksHigh = (gridHeight + TilesPerChunk - 1) / TilesPerChunk;

        // Ensure we have enough chunks
        if (chunks.Length < chunksHigh || chunks[0].Length < chunksWide)
        {
            // Insufficient chunks - use procedural fallback
            System.Diagnostics.Debug.WriteLine(
                $"Warning: Insufficient chunks ({chunks.Length}x{(chunks.Length > 0 ? chunks[0].Length : 0)}) " +
                $"for grid ({chunksWide}x{chunksHigh}). Using procedural fallback.");
            var rng = new Random(42);
            return BuildProceduralFallback(gridWidth, gridHeight, rng);
        }

        // Tile chunks into surface
        for (var cy = 0; cy < chunksHigh && cy < chunks.Length; cy++)
        {
            for (var cx = 0; cx < chunksWide && cx < chunks[cy].Length; cx++)
            {
                TileChunkIntoSurface(surface, chunks[cy][cx], cx, cy, gridWidth, gridHeight);
                cancellationToken.ThrowIfCancellationRequested();
            }
        }

        // Build zones and liquid layers
        var zones = DefineZones(gridWidth, gridHeight, _townDesign ?? CreateDefaultDesign(), 0);
        var liquid = BuildLiquidLayer(gridWidth, gridHeight, 
            _townDesign?.Hazards ?? new List<EnvironmentalHazard>(), zones);

        // For now, store spatial spec in result (Phase 3 will add persistence)
        var layout = new TownLayout { Width = gridWidth, Height = gridHeight, Surface = surface, Liquid = liquid };
        return new TownMapResult { Layout = layout, Buildings = new List<PlacedBuilding>(), Props = new List<PlacedProp>(), Zones = zones };
    }

    /// <summary>Tile a single chunk's data into the surface grid.</summary>
    private static void TileChunkIntoSurface(
        int[][] surface, TownChunk chunk, int chunkX, int chunkY,
        int gridWidth, int gridHeight)
    {
        var baseX = chunkX * TilesPerChunk;
        var baseY = chunkY * TilesPerChunk;

        // Copy chunk tile data into surface (chunk format assumed to be int[] per row)
        if (chunk?.TileData == null) return;

        for (var cy = 0; cy < TilesPerChunk; cy++)
        {
            var surfaceY = baseY + cy;
            if (surfaceY >= gridHeight) break;

            var chunkRow = chunk.TileData[cy];
            if (chunkRow == null) continue;

            for (var cx = 0; cx < TilesPerChunk; cx++)
            {
                var surfaceX = baseX + cx;
                if (surfaceX >= gridWidth) break;
                if (cx < chunkRow.Length)
                    surface[surfaceY][surfaceX] = chunkRow[cx];
            }
        }
    }

    /// <summary>Build procedural fallback when spatial spec is incomplete or insufficient.</summary>
    private static TownMapResult BuildProceduralFallback(int width, int height, Random rng)
    {
        var surface = BuildSurface(width, height, rng);
        var roadTiles = SnapRoads(width, height, "grid", rng);
        ApplyRoadTiles(surface, roadTiles);

        var buildings = new List<PlacedBuilding>();
        var occupied = new HashSet<(int, int)>();
        var props = PlaceProps(width, height, rng, occupied, 70, 30);

        var design = CreateDefaultDesign();
        var zones = DefineZones(width, height, design, 0);
        var liquid = BuildLiquidLayer(width, height, design.Hazards, zones);

        var layout = new TownLayout { Width = width, Height = height, Surface = surface, Liquid = liquid };
        return new TownMapResult { Layout = layout, Buildings = buildings, Props = props, Zones = zones };
    }

    /// <summary>Create a minimal default design for fallback scenarios.</summary>
    private static TownDesign CreateDefaultDesign() => new()
    {
        TownName = "DefaultTown",
        Landmarks = new List<LandmarkBuilding>(),
        KeyLocations = new List<KeyLocation>(),
        LayoutStyle = "grid",
        Hazards = new List<EnvironmentalHazard>(),
    };
}

/// <summary>Represents a chunk of town tile data.</summary>
public sealed record TownChunk(
    int ChunkX,
    int ChunkY,
    int[][] TileData);  // 16x16 tile data, outer array is rows (Y), inner is columns (X)

