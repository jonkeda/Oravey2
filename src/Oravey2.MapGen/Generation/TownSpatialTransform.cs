using System.Numerics;

namespace Oravey2.MapGen.Generation;

/// Transforms spatial specification to tile-based placement data
public sealed class TownSpatialTransform
{
    private readonly GeoToTileTransform _geoTransform;
    private readonly TownSpatialSpecification _spatialSpec;
    private readonly Random _rng;

    public TownSpatialTransform(
        TownSpatialSpecification spec,
        float tileSizeMeters,
        int seed,
        int maxGridDimension = 400)
    {
        _spatialSpec = spec;
        _geoTransform = new GeoToTileTransform(spec.RealWorldBounds, tileSizeMeters, maxGridDimension);
        _rng = new Random(seed);
    }

    /// Convert all building placements to tile coordinates
    public Dictionary<string, WorldTilePlacement> TransformBuildingPlacements()
    {
        var placements = new Dictionary<string, WorldTilePlacement>();

        foreach (var (name, placement) in _spatialSpec.BuildingPlacements)
        {
            var tileCenter = _geoTransform.ToTileCoord(placement.CenterLat, placement.CenterLon);
            var (widthTiles, depthTiles) = _geoTransform.FootprintToTiles(
                placement.WidthMeters, placement.DepthMeters);

            placements[name] = new WorldTilePlacement(
                CenterX: (int)Math.Round(tileCenter.X),
                CenterZ: (int)Math.Round(tileCenter.Y),
                WidthTiles: widthTiles,
                DepthTiles: depthTiles,
                RotationDegrees: placement.RotationDegrees,
                AlignmentHint: placement.AlignmentHint
            );
        }

        return placements;
    }

    /// Convert road network to tile-based segments
    public List<TileRoadSegment> TransformRoadNetwork()
    {
        var tileRoads = new List<TileRoadSegment>();

        foreach (var edge in _spatialSpec.RoadNetwork.Edges)
        {
            var from = _geoTransform.ToTileCoord(edge.FromLat, edge.FromLon);
            var to = _geoTransform.ToTileCoord(edge.ToLat, edge.ToLon);

            int roadWidthTiles = Math.Max(1, 
                (int)Math.Round(_spatialSpec.RoadNetwork.RoadWidthMeters / _geoTransform.TileSizeMeters));

            tileRoads.Add(new TileRoadSegment(
                From: from,
                To: to,
                WidthTiles: roadWidthTiles
            ));
        }

        return tileRoads;
    }

    /// Convert water bodies to tile-based polygons
    public List<TileWaterBody> TransformWaterBodies()
    {
        var tileWaters = new List<TileWaterBody>();

        foreach (var water in _spatialSpec.WaterBodies)
        {
            var tilePolygon = water.Polygon
                .Select(coord => _geoTransform.ToTileCoord(coord.X, coord.Y))
                .ToList();

            tileWaters.Add(new TileWaterBody(
                Name: water.Name,
                Polygon: tilePolygon,
                Type: water.Type
            ));
        }

        return tileWaters;
    }

    /// Get the grid dimensions
    public (int WidthTiles, int HeightTiles) GetGridDimensions() =>
        _geoTransform.GetGridDimensions();

    /// Get the geospatial transform
    public GeoToTileTransform GeoTransform => _geoTransform;
}

/// A building placement in world tile coordinates
public sealed record WorldTilePlacement(
    int CenterX,
    int CenterZ,
    int WidthTiles,
    int DepthTiles,
    double RotationDegrees,
    string AlignmentHint
);

/// A road segment in tile coordinates
public sealed record TileRoadSegment(
    Vector2 From,
    Vector2 To,
    int WidthTiles
);

/// A water body in tile coordinates
public sealed record TileWaterBody(
    string Name,
    List<Vector2> Polygon,
    SpatialWaterType Type
);
