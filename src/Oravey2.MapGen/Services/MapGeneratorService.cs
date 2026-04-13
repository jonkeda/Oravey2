using System.Diagnostics;
using System.Text;
using GitHub.Copilot.SDK;
using Oravey2.Core.World;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Models;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.Tools;

namespace Oravey2.MapGen.Services;

/// <summary>
/// Orchestrates town map generation with support for both spatial specification-based
/// and procedural generation paths. Routes based on availability of spatial specs and
/// user configuration preferences.
/// </summary>
public sealed class MapGeneratorService : IAsyncDisposable
{
    private readonly IAssetRegistry _assets;
    private CopilotClient? _client;
    private bool _preferSpatialSpecs = true;
    private Action<string>? _logger;

    public event Action<GenerationProgress>? OnProgress;

    public string? CliPath { get; set; }
    public string? ProviderType { get; set; }
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public bool UseBYOK { get; set; }

    /// <summary>
    /// Optional override for the asset registry. When set, this is used instead
    /// of the default embedded catalog. Set from content pack catalog path.
    /// </summary>
    public IAssetRegistry? AssetRegistryOverride { get; set; }

    private IAssetRegistry EffectiveAssets => AssetRegistryOverride ?? _assets;

    public MapGeneratorService(IAssetRegistry assets)
    {
        _assets = assets;
    }

    /// <summary>
    /// Set the preference for using spatial specifications when available.
    /// </summary>
    public void SetPreferSpatialSpecs(bool prefer)
    {
        _preferSpatialSpecs = prefer;
    }

    /// <summary>
    /// Set optional logging callback for generation decisions.
    /// </summary>
    public void SetLogger(Action<string>? logger)
    {
        _logger = logger;
    }

    public Task<GenerationResult> GenerateAsync(
        MapGenerationRequest request,
        CancellationToken ct = default)
    {
        // Old LLM blueprint pipeline removed — will be replaced in Step 09 (procedural generation).
        return Task.FromResult(new GenerationResult
        {
            Success = false,
            ErrorMessage = "Blueprint generation pipeline has been removed. Procedural generation coming soon.",
            Elapsed = TimeSpan.Zero
        });
    }

    public Task<GenerationResult> RefineAsync(
        string sessionId,
        string refinementPrompt,
        CancellationToken ct = default)
    {
        return Task.FromResult(new GenerationResult
        {
            Success = false,
            ErrorMessage = "Blueprint generation pipeline has been removed.",
            SessionId = sessionId,
            Elapsed = TimeSpan.Zero
        });
    }

    /// <summary>
    /// Generate a town map, routing to spatial spec or procedural path based on design and preferences.
    /// </summary>
    public async Task<TownMapResult> GenerateTownMapAsync(
        TownDesign design,
        CuratedTown town,
        TownEntry townEntry,
        RegionTemplate region,
        MapGenerationParams generationParams,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            // Decision: use spatial spec path or procedural?
            if (design.SpatialSpec != null && _preferSpatialSpecs)
            {
                _logger?.Invoke(LogSpatialSpecDecision(design.SpatialSpec));
                return await GenerateWithSpatialSpecAsync(design, town, townEntry, region, generationParams, ct);
            }
            else
            {
                var reason = design.SpatialSpec == null ? "no spatial spec available" : "spatial specs disabled";
                _logger?.Invoke($"[MAPGEN] Falling back to procedural generation ({reason})");
                return await GenerateProceduralAsync(design, town, townEntry, region, generationParams, ct);
            }
        }
        catch (Exception ex)
        {
            _logger?.Invoke($"[MAPGEN] Error during generation: {ex.Message}. Attempting procedural fallback.");
            try
            {
                return await GenerateProceduralAsync(design, town, townEntry, region, generationParams, ct);
            }
            catch (Exception fallbackEx)
            {
                _logger?.Invoke($"[MAPGEN] Procedural fallback also failed: {fallbackEx.Message}");
                throw;
            }
        }
    }

    /// <summary>
    /// Generate using spatial specification path.
    /// </summary>
    private async Task<TownMapResult> GenerateWithSpatialSpecAsync(
        TownDesign design,
        CuratedTown town,
        TownEntry townEntry,
        RegionTemplate region,
        MapGenerationParams generationParams,
        CancellationToken ct)
    {
        await Task.CompletedTask; // No async work yet, but structured for future enhancements
        
        // Create spatial transform from spec
        var spatialSpec = design.SpatialSpec!;
        var tileSizeMeters = 1.0f; // Standard tile size
        var seed = generationParams.Seed ?? Random.Shared.Next();
        
        var spatialTransform = new TownSpatialTransform(
            spatialSpec,
            tileSizeMeters,
            seed,
            maxGridDimension: 400);

        var (gridWidth, gridHeight) = spatialTransform.GetGridDimensions();

        // Generate chunks using spatial spec
        var generator = new TownChunkGenerator();
        var chunksWide = (gridWidth + 15) / 16;
        var chunksHigh = (gridHeight + 15) / 16;
        
        var chunks = new TownChunk[chunksHigh][];
        for (int cy = 0; cy < chunksHigh; cy++)
        {
            chunks[cy] = new TownChunk[chunksWide];
            for (int cx = 0; cx < chunksWide; cx++)
            {
                var chunkResult = generator.GenerateWithSpatialSpec(
                    spatialTransform, town, townEntry,
                    cx, cy, region, seed);
                
                chunks[cy][cx] = new TownChunk(
                    ChunkX: cx,
                    ChunkY: cy,
                    TileData: chunkResult.Tiles.ToTileArray());
                
                ct.ThrowIfCancellationRequested();
            }
        }

        // Condense chunks to final map
        var condenser = new TownMapCondenser(design);
        var result = condenser.CondenseWithSpatialSpec(chunks, spatialTransform, ct);

        // Attach spatial spec to result for persistence
        return TownMapResult.CreateWithSerializedSpec(
            result.Layout,
            result.Buildings,
            result.Props,
            result.Zones,
            spatialSpec);
    }

    /// <summary>
    /// Generate using procedural (non-spatial) path.
    /// </summary>
    private async Task<TownMapResult> GenerateProceduralAsync(
        TownDesign design,
        CuratedTown town,
        TownEntry townEntry,
        RegionTemplate region,
        MapGenerationParams generationParams,
        CancellationToken ct)
    {
        await Task.CompletedTask; // No async work yet
        
        var condenser = new TownMapCondenser(design);
        return condenser.Condense(town, design, region, generationParams);
    }

    /// <summary>
    /// Format spatial spec decision log message with statistics.
    /// </summary>
    private static string LogSpatialSpecDecision(TownSpatialSpecification spec)
    {
        var buildingCount = spec.BuildingPlacements.Count;
        var roadCount = spec.RoadNetwork.Edges.Count;
        var waterCount = spec.WaterBodies.Count;
        
        var bounds = spec.RealWorldBounds;
        var latDelta = bounds.MaxLat - bounds.MinLat;
        var lonDelta = bounds.MaxLon - bounds.MinLon;
        
        return $"[MAPGEN] Generating town with spatial spec " +
               $"({buildingCount} buildings, {roadCount} roads, {waterCount} water bodies) " +
               $"bounds: {latDelta:F4}°×{lonDelta:F4}°";
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            try { await _client.StopAsync(); } catch { }
            _client = null;
        }
    }
}

/// <summary>Helper to convert TileMapData to array format for chunk storage.</summary>
internal static class TileMapDataExtensions
{
    public static int[][] ToTileArray(this TileMapData tileData)
    {
        var result = new int[16][];
        for (int y = 0; y < 16; y++)
        {
            result[y] = new int[16];
            for (int x = 0; x < 16; x++)
            {
                var tile = tileData.GetTileData(x, y);
                result[y][x] = (int)tile.Surface;
            }
        }
        return result;
    }
}
