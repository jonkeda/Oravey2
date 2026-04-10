using System.Numerics;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Generates compact game-scale tile maps for a designed town by condensing
/// real-world OSM geometry into a playable grid.
/// </summary>
public sealed class TownMapCondenser
{
    private const int TilesPerChunk = 16;

    private static readonly string[] PropAssets =
    [
        "meshes/barrel.glb",
        "meshes/crate.glb",
        "meshes/vehicle_wreck.glb",
        "meshes/trash_pile.glb",
        "meshes/sandbags.glb",
    ];

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

        var landmark = PlaceLandmark(design.Landmark, centreX, centreY, buildings.Count, occupied);
        buildings.Add(landmark);

        // Step 5: Place key locations along main roads
        PlaceKeyLocations(design.KeyLocations, roadTiles, width, height, rng, buildings, occupied);

        // Step 6: Fill with generic buildings
        FillGenericBuildings(width, height, rng, buildings, occupied, parms.BuildingFillPercent);

        // Step 7: Place props
        var props = PlaceProps(width, height, rng, occupied, parms.PropDensityPercent, parms.MaxProps);

        // Step 8: Define zones from hazards
        var zones = DefineZones(width, height, design, town.ThreatLevel);

        var layout = new TownLayout(width, height, surface);
        return new TownMapResult(layout, buildings, props, zones);
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
        var locationCount = design.KeyLocations.Count + 1; // +1 for landmark
        var baseDim = Math.Max(16, locationCount * 4);
        baseDim = Math.Min(baseDim, 48);

        // Round up to chunk boundaries
        var chunks = (baseDim + TilesPerChunk - 1) / TilesPerChunk;
        var dim = chunks * TilesPerChunk;
        return (dim, dim);
    }

    internal static int[][] BuildSurface(int width, int height, Random rng)
    {
        // Surface type IDs: 0 = dirt, 1 = grass, 2 = concrete, 3 = gravel
        var surface = new int[height][];
        for (var y = 0; y < height; y++)
        {
            surface[y] = new int[width];
            for (var x = 0; x < width; x++)
                surface[y][x] = rng.Next(0, 2); // mostly dirt/grass
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
                surface[y][x] = 2; // concrete
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

        return new PlacedBuilding(
            $"building_{index}",
            landmark.Name,
            $"meshes/{landmark.Name.ToLowerInvariant().Replace(' ', '_')}.glb",
            landmark.SizeCategory,
            footprint,
            floors,
            0.6f,
            new TilePlacement(tileX / TilesPerChunk, tileY / TilesPerChunk,
                              tileX % TilesPerChunk, tileY % TilesPerChunk));
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

                buildings.Add(new PlacedBuilding(
                    $"building_{buildings.Count}",
                    loc.Name,
                    $"meshes/{loc.Name.ToLowerInvariant().Replace(' ', '_')}.glb",
                    loc.SizeCategory,
                    footprint,
                    floors,
                    rng.NextSingle() * 0.4f + 0.4f,
                    new TilePlacement(cx / TilesPerChunk, cy / TilesPerChunk,
                                      cx % TilesPerChunk, cy % TilesPerChunk)));

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

                        buildings.Add(new PlacedBuilding(
                            $"building_{buildings.Count}",
                            loc.Name,
                            $"meshes/{loc.Name.ToLowerInvariant().Replace(' ', '_')}.glb",
                            loc.SizeCategory,
                            footprint,
                            floors,
                            rng.NextSingle() * 0.4f + 0.4f,
                            new TilePlacement(x / TilesPerChunk, y / TilesPerChunk,
                                              x % TilesPerChunk, y % TilesPerChunk)));
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

                buildings.Add(new PlacedBuilding(
                    $"building_{buildings.Count}",
                    $"Ruin {buildings.Count}",
                    "meshes/generic_ruin.glb",
                    "small",
                    footprint,
                    1,
                    rng.NextSingle() * 0.3f + 0.2f,
                    new TilePlacement(x / TilesPerChunk, y / TilesPerChunk,
                                      x % TilesPerChunk, y % TilesPerChunk)));
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
                props.Add(new PlacedProp(
                    $"prop_{i}",
                    PropAssets[rng.Next(PropAssets.Length)],
                    new TilePlacement(x / TilesPerChunk, y / TilesPerChunk,
                                      x % TilesPerChunk, y % TilesPerChunk),
                    rng.NextSingle() * 360f,
                    0.8f + rng.NextSingle() * 0.4f,
                    rng.Next(3) == 0));
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

        zones.Add(new TownZone(
            "zone_main",
            design.TownName,
            0, // default biome
            threatLevel > 5 ? 0.2f : 0f,
            Math.Clamp(threatLevel / 3, 1, 5),
            true,
            0, 0, chunksWide - 1, chunksHigh - 1));

        // Add hazard zones
        for (var i = 0; i < design.Hazards.Count; i++)
        {
            var hazard = design.Hazards[i];
            // Place hazard zones at edges based on location hint
            var (sx, sy, ex, ey) = HazardBounds(hazard.LocationHint, chunksWide, chunksHigh);
            zones.Add(new TownZone(
                $"zone_hazard_{i}",
                $"{hazard.Type} zone",
                1,
                hazard.Type.Contains("radiation", StringComparison.OrdinalIgnoreCase) ? 0.5f : 0.1f,
                Math.Clamp(threatLevel / 2, 1, 5),
                false,
                sx, sy, ex, ey));
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
}
