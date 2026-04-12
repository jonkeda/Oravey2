using System.Numerics;

namespace Oravey2.MapGen.Generation;

/// Transforms real-world geographic coordinates to game tile coordinates
public sealed class GeoToTileTransform
{
    private readonly BoundingBox _realWorldBounds;
    private readonly float _tileSizeMeters;
    private readonly int _gridWidthTiles;
    private readonly int _gridHeightTiles;

    /// Create a transform from real-world bounds and tile size
    public GeoToTileTransform(
        BoundingBox realWorldBounds,
        float tileSizeMeters,
        int maxGridDimension = 400)
    {
        _realWorldBounds = realWorldBounds;
        _tileSizeMeters = tileSizeMeters;

        // Compute grid dimensions from real-world bounds
        double latRange = realWorldBounds.MaxLat - realWorldBounds.MinLat;
        double lonRange = realWorldBounds.MaxLon - realWorldBounds.MinLon;

        // Approximate meters per degree (varies by latitude, but use average)
        const double metersPerDegreeLat = 111320.0;
        double metersPerDegreeLon = metersPerDegreeLat * Math.Cos(
            (realWorldBounds.MinLat + realWorldBounds.MaxLat) / 2.0 * Math.PI / 180.0);

        double heightMeters = latRange * metersPerDegreeLat;
        double widthMeters = lonRange * metersPerDegreeLon;

        _gridHeightTiles = (int)Math.Ceiling(heightMeters / tileSizeMeters);
        _gridWidthTiles = (int)Math.Ceiling(widthMeters / tileSizeMeters);

        // Clamp to reasonable limits
        _gridWidthTiles = Math.Clamp(_gridWidthTiles, 50, maxGridDimension);
        _gridHeightTiles = Math.Clamp(_gridHeightTiles, 50, maxGridDimension);
    }

    /// Convert real-world (lat, lon) to tile coordinates (x, z)
    public Vector2 ToTileCoord(double lat, double lon)
    {
        // Normalize coordinates to [0, 1]
        double normLat = (lat - _realWorldBounds.MinLat) / 
                         (_realWorldBounds.MaxLat - _realWorldBounds.MinLat);
        double normLon = (lon - _realWorldBounds.MinLon) / 
                         (_realWorldBounds.MaxLon - _realWorldBounds.MinLon);

        // Clamp to valid range
        normLat = Math.Clamp(normLat, 0.0, 1.0);
        normLon = Math.Clamp(normLon, 0.0, 1.0);

        // Map to tile grid
        float tileX = (float)(normLon * _gridWidthTiles);
        float tileZ = (float)(normLat * _gridHeightTiles);

        return new Vector2(tileX, tileZ);
    }

    /// Convert tile coordinates (x, z) back to real-world (lat, lon)
    public (double Lat, double Lon) FromTileCoord(float tileX, float tileZ)
    {
        // Normalize tile coordinates to [0, 1]
        double normLon = tileX / _gridWidthTiles;
        double normLat = tileZ / _gridHeightTiles;

        // Clamp to valid range
        normLon = Math.Clamp(normLon, 0.0, 1.0);
        normLat = Math.Clamp(normLat, 0.0, 1.0);

        // Map to geo coordinates
        double lat = _realWorldBounds.MinLat + 
                     normLat * (_realWorldBounds.MaxLat - _realWorldBounds.MinLat);
        double lon = _realWorldBounds.MinLon + 
                     normLon * (_realWorldBounds.MaxLon - _realWorldBounds.MinLon);

        return (lat, lon);
    }

    /// Convert a building footprint size from meters to tiles
    public (int WidthTiles, int DepthTiles) FootprintToTiles(double widthMeters, double depthMeters)
    {
        int widthTiles = (int)Math.Ceiling(widthMeters / _tileSizeMeters);
        int depthTiles = (int)Math.Ceiling(depthMeters / _tileSizeMeters);

        // Minimum 1 tile per dimension
        widthTiles = Math.Max(1, widthTiles);
        depthTiles = Math.Max(1, depthTiles);

        return (widthTiles, depthTiles);
    }

    /// Get the grid dimensions (total tiles)
    public (int WidthTiles, int HeightTiles) GetGridDimensions()
    {
        return (_gridWidthTiles, _gridHeightTiles);
    }

    /// Get the tile size in meters
    public float TileSizeMeters => _tileSizeMeters;

    /// Get the real-world bounds
    public BoundingBox RealWorldBounds => _realWorldBounds;
}
