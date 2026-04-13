using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Oravey2.Contracts.Spatial;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Core.World.Spatial;

/// <summary>
/// Renders buildings, roads, and water bodies from a TownSpatialSpecification.
/// Handles coordinate transformation from real-world lat/lon to game tile coordinates,
/// applies collision detection, and manages z-ordering for rendering.
/// </summary>
public sealed class SpatialSpecRenderer
{
    private readonly ILogger<SpatialSpecRenderer> _logger;

    public SpatialSpecRenderer(ILogger<SpatialSpecRenderer>? logger = null)
    {
        _logger = logger ?? NullLogger<SpatialSpecRenderer>.Instance;
    }

    /// <summary>
    /// Renders a spatial specification onto a tile map.
    /// Returns rendering results including collision records and statistics.
    /// </summary>
    public SpatialSpecRenderingResult Render(
        TownSpatialSpecification spec,
        TileMapData mapData,
        float tileSize = 1.0f)
    {
        var collisions = new List<CollisionRecord>();

        var result = new SpatialSpecRenderingResult();

        // Transform real-world coordinates to tile coordinates
        var transformer = new GeoToTileTransformer(spec.RealWorldBounds, mapData.Width, mapData.Height, tileSize);

        // 1. Render terrain first (base layer)
        result.TerrainStats = RenderTerrain(mapData, spec, transformer);

        // 2. Render water second (no occlusion)
        result.WaterStats = RenderWater(mapData, spec, transformer, collisions);

        // 3. Render roads third
        result.RoadStats = RenderRoads(mapData, spec, transformer, collisions);

        // 4. Render buildings last (topmost layer)
        result.BuildingStats = RenderBuildings(mapData, spec, transformer, collisions);

        result.CollisionCount = collisions.Count;
        result.Collisions = collisions;

        _logger.LogInformation(
            "Rendered spatial spec: {Buildings} buildings, {Roads} road segments, {Water} water bodies",
            result.BuildingStats.Count,
            result.RoadStats.SegmentCount,
            result.WaterStats.Count);

        if (collisions.Count > 0)
        {
            _logger.LogWarning("Found {Count} collisions during rendering", collisions.Count);
            foreach (var collision in collisions)
                _logger.LogWarning("[COLLISION] {Message}", collision.Message);
        }

        return result;
    }

    private TerrainRenderingStats RenderTerrain(
        TileMapData mapData,
        TownSpatialSpecification spec,
        GeoToTileTransformer transformer)
    {
        var stats = new TerrainRenderingStats
        {
            Description = spec.TerrainDescription,
            Timestamp = DateTime.UtcNow
        };

        _logger.LogInformation("Terrain description: {Description}", spec.TerrainDescription);

        return stats;
    }

    private WaterRenderingStats RenderWater(
        TileMapData mapData,
        TownSpatialSpecification spec,
        GeoToTileTransformer transformer,
        List<CollisionRecord> collisions)
    {
        var stats = new WaterRenderingStats
        {
            Count = spec.WaterBodies.Count,
            Timestamp = DateTime.UtcNow
        };

        foreach (var water in spec.WaterBodies)
        {
            var waterTiles = transformer.PolygonToTiles(water.Polygon);
            stats.TilesRendered += waterTiles.Count;

            foreach (var (tx, ty) in waterTiles)
            {
                if (IsInBounds(tx, ty, mapData))
                {
                    var existing = mapData.GetTileData(tx, ty);
                    var waterTile = TileDataFactory.Water();
                    mapData.SetTileData(tx, ty, waterTile);

                    // Check for collisions with buildings
                    if (existing.StructureId != 0)
                    {
                        collisions.Add(new CollisionRecord(
                            $"Water '{water.Name}' overlaps building structure at tile ({tx}, {ty})",
                            CollisionType.WaterBuildingOverlap));
                    }
                }
            }

            _logger.LogInformation(
                "Rendered water body '{Name}' ({Type}): {Count} tiles",
                water.Name, water.Type, waterTiles.Count);
        }

        return stats;
    }

    private RoadRenderingStats RenderRoads(
        TileMapData mapData,
        TownSpatialSpecification spec,
        GeoToTileTransformer transformer,
        List<CollisionRecord> collisions)
    {
        var stats = new RoadRenderingStats
        {
            SegmentCount = spec.RoadNetwork.Edges.Count,
            Timestamp = DateTime.UtcNow
        };

        foreach (var edge in spec.RoadNetwork.Edges)
        {
            var roadTiles = transformer.LineToTiles(edge.FromLat, edge.FromLon, edge.ToLat, edge.ToLon);
            stats.TilesRendered += roadTiles.Count;

            foreach (var (tx, ty) in roadTiles)
            {
                if (IsInBounds(tx, ty, mapData))
                {
                    var existing = mapData.GetTileData(tx, ty);
                    var roadTile = TileDataFactory.Road();
                    mapData.SetTileData(tx, ty, roadTile);

                    // Check for collisions with buildings
                    if (existing.StructureId != 0)
                    {
                        collisions.Add(new CollisionRecord(
                            $"Road overlaps building structure at tile ({tx}, {ty})",
                            CollisionType.RoadBuildingOverlap));
                    }
                }
            }
        }

        _logger.LogInformation(
            "Rendered road network: {Segments} segments, {Tiles} tiles",
            spec.RoadNetwork.Edges.Count, stats.TilesRendered);

        return stats;
    }

    private BuildingRenderingStats RenderBuildings(
        TileMapData mapData,
        TownSpatialSpecification spec,
        GeoToTileTransformer transformer,
        List<CollisionRecord> collisions)
    {
        var stats = new BuildingRenderingStats
        {
            Count = spec.BuildingPlacements.Count,
            Timestamp = DateTime.UtcNow
        };

        foreach (var (buildingName, placement) in spec.BuildingPlacements)
        {
            var buildingTiles = transformer.RectangleToTiles(
                placement.CenterLat, placement.CenterLon,
                placement.WidthMeters, placement.DepthMeters,
                placement.RotationDegrees);

            var rotation = NormalizeRotation(placement.RotationDegrees);
            stats.TilesRendered += buildingTiles.Count;

            foreach (var (tx, ty) in buildingTiles)
            {
                if (IsInBounds(tx, ty, mapData))
                {
                    var existing = mapData.GetTileData(tx, ty);

                    // Check for collision with existing structure or water
                    if (existing.StructureId != 0)
                    {
                        collisions.Add(new CollisionRecord(
                            $"Building '{buildingName}' overlaps existing structure at tile ({tx}, {ty})",
                            CollisionType.BuildingBuildingOverlap));
                    }
                    if (existing.WaterLevel > 0)
                    {
                        collisions.Add(new CollisionRecord(
                            $"Building '{buildingName}' overlaps water at tile ({tx}, {ty})",
                            CollisionType.BuildingWaterOverlap));
                    }

                    int structureId = DeterministicHash(buildingName);
                    if (structureId == 0) structureId = 1;

                    var buildingTile = new TileData(
                        existing.Surface,
                        existing.HeightLevel,
                        existing.WaterLevel,
                        structureId,
                        existing.Flags & ~TileFlags.Walkable,
                        existing.VariantSeed);
                    mapData.SetTileData(tx, ty, buildingTile);
                }
            }

            _logger.LogInformation(
                "Rendered building '{Name}': {Count} tiles, rotation: {Rotation}°",
                buildingName, buildingTiles.Count, rotation);
        }

        return stats;
    }

    private static bool IsInBounds(int tx, int ty, TileMapData mapData)
        => tx >= 0 && tx < mapData.Width && ty >= 0 && ty < mapData.Height;

    private static float NormalizeRotation(double degrees)
    {
        var normalized = degrees % 360;
        if (normalized < 0) normalized += 360;
        return (float)normalized;
    }

    /// <summary>
    /// FNV-1a hash — deterministic across processes, machines, and .NET versions.
    /// </summary>
    internal static int DeterministicHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (char c in text)
            {
                hash ^= c;
                hash *= 16777619;
            }
            return (int)hash;
        }
    }
}

/// <summary>
/// Transforms geographic (lat/lon) coordinates to game tile coordinates.
/// </summary>
public sealed class GeoToTileTransformer
{
    private readonly BoundingBox _bounds;
    private readonly int _mapWidth;
    private readonly int _mapHeight;
    private readonly float _tileSize;
    private readonly double _latRange;
    private readonly double _lonRange;

    public GeoToTileTransformer(BoundingBox bounds, int mapWidth, int mapHeight, float tileSize)
    {
        _bounds = bounds;
        _mapWidth = mapWidth;
        _mapHeight = mapHeight;
        _tileSize = tileSize;
        _latRange = bounds.MaxLat - bounds.MinLat;
        _lonRange = bounds.MaxLon - bounds.MinLon;
    }

    public (int TileX, int TileY) GeoToTile(double lat, double lon)
    {
        double latNorm = (lat - _bounds.MinLat) / _latRange;
        double lonNorm = (lon - _bounds.MinLon) / _lonRange;

        int tx = (int)(lonNorm * _mapWidth);
        int ty = (int)(latNorm * _mapHeight);

        return (tx, ty);
    }

    public List<(int TileX, int TileY)> PolygonToTiles(List<Vector2> polygon)
    {
        if (polygon.Count < 3) return new List<(int, int)>();

        // Convert polygon vertices to tile coordinates
        var tileVertices = polygon
            .Select(p => GeoToTile(p.X, p.Y))
            .ToList();

        return ScanlineFill(tileVertices);
    }

    public List<(int TileX, int TileY)> LineToTiles(double lat1, double lon1, double lat2, double lon2)
    {
        var (x1, y1) = GeoToTile(lat1, lon1);
        var (x2, y2) = GeoToTile(lat2, lon2);

        return BresenhamLine(x1, y1, x2, y2);
    }

    public List<(int TileX, int TileY)> RectangleToTiles(
        double centerLat, double centerLon,
        double widthMeters, double depthMeters,
        double rotationDegrees)
    {
        // Convert building dimensions from meters to degrees (rough approximation)
        double metersPerLatDegree = 111000.0;
        double centerLatRadians = centerLat * System.Math.PI / 180.0;
        double metersPerLonDegree = 111000.0 * System.Math.Cos(centerLatRadians);

        double halfWidthDegrees = (widthMeters / 2) / metersPerLonDegree;
        double halfDepthDegrees = (depthMeters / 2) / metersPerLatDegree;

        // Generate corner points with rotation, then delegate to polygon fill
        var corners = GetRotatedRectangleCorners(
            centerLat, centerLon, halfWidthDegrees, halfDepthDegrees, rotationDegrees);

        return PolygonToTiles(corners);
    }

    /// <summary>
    /// Scanline fill algorithm: for each row in the tile-space bounding box,
    /// find edge intersections and fill between pairs.
    /// </summary>
    private static List<(int TileX, int TileY)> ScanlineFill(List<(int TileX, int TileY)> tileVertices)
    {
        var tiles = new HashSet<(int, int)>();

        int minX = tileVertices.Min(v => v.TileX);
        int maxX = tileVertices.Max(v => v.TileX);
        int minY = tileVertices.Min(v => v.TileY);
        int maxY = tileVertices.Max(v => v.TileY);

        for (int y = minY; y <= maxY; y++)
        {
            var intersections = new List<int>();

            for (int i = 0; i < tileVertices.Count; i++)
            {
                var (x1, y1) = tileVertices[i];
                var (x2, y2) = tileVertices[(i + 1) % tileVertices.Count];

                if ((y1 <= y && y2 > y) || (y2 <= y && y1 > y))
                {
                    int xIntersect = x1 + (y - y1) * (x2 - x1) / (y2 - y1);
                    intersections.Add(xIntersect);
                }
            }

            intersections.Sort();

            // Fill between pairs of intersections
            for (int j = 0; j + 1 < intersections.Count; j += 2)
            {
                for (int x = intersections[j]; x <= intersections[j + 1]; x++)
                {
                    tiles.Add((x, y));
                }
            }
        }

        return tiles.ToList();
    }

    private List<Vector2> GetRotatedRectangleCorners(
        double centerLat, double centerLon,
        double halfWidthDegrees, double halfDepthDegrees,
        double rotationDegrees)
    {
        var radians = rotationDegrees * System.Math.PI / 180.0;
        var cos = System.Math.Cos(radians);
        var sin = System.Math.Sin(radians);

        // Corners relative to center
        var corners = new[]
        {
            (-halfWidthDegrees, -halfDepthDegrees),
            (halfWidthDegrees, -halfDepthDegrees),
            (halfWidthDegrees, halfDepthDegrees),
            (-halfWidthDegrees, halfDepthDegrees),
        };

        var result = new List<Vector2>();
        foreach (var (dx, dy) in corners)
        {
            var rotX = dx * cos - dy * sin;
            var rotY = dx * sin + dy * cos;
            // Convention: X=latitude, Y=longitude (matches all other Vector2 usage in spatial code)
            result.Add(new Vector2((float)(centerLat + rotY), (float)(centerLon + rotX)));
        }

        return result;
    }

    private static List<(int TileX, int TileY)> BresenhamLine(int x0, int y0, int x1, int y1)
    {
        var tiles = new List<(int, int)>();
        int dx = System.Math.Abs(x1 - x0);
        int dy = System.Math.Abs(y1 - y0);
        int sx = x0 < x1 ? 1 : -1;
        int sy = y0 < y1 ? 1 : -1;
        int err = dx - dy;

        while (true)
        {
            tiles.Add((x0, y0));
            if (x0 == x1 && y0 == y1) break;

            int e2 = 2 * err;
            if (e2 > -dy)
            {
                err -= dy;
                x0 += sx;
            }
            if (e2 < dx)
            {
                err += dx;
                y0 += sy;
            }
        }

        return tiles;
    }
}

/// <summary>
/// Represents a collision during spatial spec rendering.
/// </summary>
public sealed record CollisionRecord(string Message, CollisionType Type);

public enum CollisionType
{
    BuildingBuildingOverlap,
    BuildingWaterOverlap,
    WaterBuildingOverlap,
    RoadBuildingOverlap,
    RoadWaterOverlap,
}

/// <summary>
/// Results of rendering a spatial specification.
/// </summary>
public sealed record SpatialSpecRenderingResult
{
    public TerrainRenderingStats? TerrainStats { get; set; }
    public WaterRenderingStats? WaterStats { get; set; }
    public RoadRenderingStats? RoadStats { get; set; }
    public BuildingRenderingStats? BuildingStats { get; set; }
    public int CollisionCount { get; set; }
    public List<CollisionRecord> Collisions { get; set; } = new();
}

public sealed record TerrainRenderingStats
{
    public string? Description { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed record WaterRenderingStats
{
    public int Count { get; set; }
    public int TilesRendered { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed record RoadRenderingStats
{
    public int SegmentCount { get; set; }
    public int TilesRendered { get; set; }
    public DateTime Timestamp { get; set; }
}

public sealed record BuildingRenderingStats
{
    public int Count { get; set; }
    public int TilesRendered { get; set; }
    public DateTime Timestamp { get; set; }
}
