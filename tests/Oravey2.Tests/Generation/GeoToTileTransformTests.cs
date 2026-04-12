using System.Numerics;
using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class GeoToTileTransformTests
{
    [Fact]
    public void ToTileCoord_CenterOfBounds_ReturnsCentroid()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);

        var center = transform.ToTileCoord(52.5, 4.5);
        var (width, height) = transform.GetGridDimensions();

        // Center should be roughly in the middle of the grid
        Assert.True(center.X > width * 0.4 && center.X < width * 0.6);
        Assert.True(center.Y > height * 0.4 && center.Y < height * 0.6);
    }

    [Fact]
    public void ToTileCoord_MinBounds_ReturnsOrigin()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);

        var min = transform.ToTileCoord(52.0, 4.0);
        
        Assert.True(min.X < 1.0f);
        Assert.True(min.Y < 1.0f);
    }

    [Fact]
    public void ToTileCoord_MaxBounds_ReturnsGridMax()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);
        var (width, height) = transform.GetGridDimensions();

        var max = transform.ToTileCoord(53.0, 5.0);
        
        Assert.True(max.X > width - 1.0f);
        Assert.True(max.Y > height - 1.0f);
    }

    [Fact]
    public void FromTileCoord_RoundTrip_RecoveryAccurate()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);

        var tile = transform.ToTileCoord(52.5, 4.5);
        var (lat, lon) = transform.FromTileCoord(tile.X, tile.Y);

        Assert.True(Math.Abs(lat - 52.5) < 0.01);
        Assert.True(Math.Abs(lon - 4.5) < 0.01);
    }

    [Fact]
    public void FootprintToTiles_Small_ReturnMinimumTile()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);

        var (w, h) = transform.FootprintToTiles(1.0, 1.5);
        
        Assert.Equal(1, w);
        Assert.Equal(1, h);
    }

    [Fact]
    public void FootprintToTiles_Large_ReturnMultipleTiles()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);

        var (w, h) = transform.FootprintToTiles(40.0, 50.0);
        
        Assert.Equal(20, w);
        Assert.Equal(25, h);
    }

    [Fact]
    public void GetGridDimensions_RespectsClamping()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f, maxGridDimension: 100);

        var (w, h) = transform.GetGridDimensions();
        
        Assert.True(w <= 100);
        Assert.True(h <= 100);
    }

    [Fact]
    public void TileSizeMeters_ReturnedCorrectly()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 3.5f);

        Assert.Equal(3.5f, transform.TileSizeMeters);
    }

    [Fact]
    public void RealWorldBounds_ReturnedCorrectly()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);

        var returned = transform.RealWorldBounds;
        
        Assert.Equal(52.0, returned.MinLat);
        Assert.Equal(53.0, returned.MaxLat);
        Assert.Equal(4.0, returned.MinLon);
        Assert.Equal(5.0, returned.MaxLon);
    }

    [Fact]
    public void ToTileCoord_WithClamping_StaysInBounds()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var transform = new GeoToTileTransform(bbox, tileSizeMeters: 2.0f);
        var (width, height) = transform.GetGridDimensions();

        // Try coordinates outside bounds
        var outside = transform.ToTileCoord(51.0, 3.0);  // Below and west
        
        Assert.True(outside.X >= 0);
        Assert.True(outside.Y >= 0);
    }
}

public class TownSpatialTransformTests
{
    [Fact]
    public void TransformBuildingPlacements_CreatesPlacementsForAllBuildings()
    {
        var spec = CreateTestSpatialSpec();
        var transform = new TownSpatialTransform(spec, tileSizeMeters: 2.0f, seed: 42);

        var placements = transform.TransformBuildingPlacements();

        Assert.Equal(2, placements.Count);
        Assert.Contains("Cathedral", placements.Keys);
        Assert.Contains("Market", placements.Keys);
    }

    [Fact]
    public void TransformBuildingPlacements_FootprintConverted()
    {
        var spec = CreateTestSpatialSpec();
        var transform = new TownSpatialTransform(spec, tileSizeMeters: 2.0f, seed: 42);

        var placements = transform.TransformBuildingPlacements();
        var cathedral = placements["Cathedral"];

        // Cathedral: 40m × 50m at 2m/tile = 20×25 tiles
        Assert.Equal(20, cathedral.WidthTiles);
        Assert.Equal(25, cathedral.DepthTiles);
    }

    [Fact]
    public void TransformBuildingPlacements_RotationPreserved()
    {
        var spec = CreateTestSpatialSpec();
        var transform = new TownSpatialTransform(spec, tileSizeMeters: 2.0f, seed: 42);

        var placements = transform.TransformBuildingPlacements();
        var cathedral = placements["Cathedral"];

        Assert.Equal(45.0, cathedral.RotationDegrees);
    }

    [Fact]
    public void TransformBuildingPlacements_AlignmentHintPreserved()
    {
        var spec = CreateTestSpatialSpec();
        var transform = new TownSpatialTransform(spec, tileSizeMeters: 2.0f, seed: 42);

        var placements = transform.TransformBuildingPlacements();
        var market = placements["Market"];

        Assert.Equal("on_main_road", market.AlignmentHint);
    }

    [Fact]
    public void TransformRoadNetwork_CreatesSegments()
    {
        var spec = CreateTestSpatialSpec();
        var transform = new TownSpatialTransform(spec, tileSizeMeters: 2.0f, seed: 42);

        var roads = transform.TransformRoadNetwork();

        Assert.Single(roads);
    }

    [Fact]
    public void TransformWaterBodies_CreatesPolygons()
    {
        var specWithWater = CreateTestSpatialSpecWithWater();
        var transform = new TownSpatialTransform(specWithWater, tileSizeMeters: 2.0f, seed: 42);

        var waters = transform.TransformWaterBodies();

        Assert.Single(waters);
        Assert.Equal("Main Canal", waters[0].Name);
        Assert.Equal(SpatialWaterType.Canal, waters[0].Type);
    }

    [Fact]
    public void GetGridDimensions_ReturnedCorrectly()
    {
        var spec = CreateTestSpatialSpec();
        var transform = new TownSpatialTransform(spec, tileSizeMeters: 2.0f, seed: 42);

        var (width, height) = transform.GetGridDimensions();

        Assert.True(width > 0);
        Assert.True(height > 0);
    }

    [Fact]
    public void GeoTransform_Accessible()
    {
        var spec = CreateTestSpatialSpec();
        var transform = new TownSpatialTransform(spec, tileSizeMeters: 2.0f, seed: 42);

        var geoTransform = transform.GeoTransform;

        Assert.NotNull(geoTransform);
        Assert.Equal(2.0f, geoTransform.TileSizeMeters);
    }

    private TownSpatialSpecification CreateTestSpatialSpec()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var placements = new Dictionary<string, BuildingPlacement>
        {
            ["Cathedral"] = new BuildingPlacement("Cathedral", 52.5, 4.5, 40.0, 50.0, 45.0, "square_corner"),
            ["Market"] = new BuildingPlacement("Market", 52.4, 4.4, 20.0, 30.0, 0.0, "on_main_road")
        };

        return new TownSpatialSpecification(
            RealWorldBounds: bbox,
            BuildingPlacements: placements,
            RoadNetwork: new RoadNetwork(
                Nodes: new List<Vector2> { new Vector2(52.5f, 4.5f), new Vector2(52.4f, 4.4f) },
                Edges: new List<RoadEdge> { new RoadEdge(52.5, 4.5, 52.4, 4.4) },
                RoadWidthMeters: 10.0f
            ),
            WaterBodies: [],
            TerrainDescription: "flat"
        );
    }

    private TownSpatialSpecification CreateTestSpatialSpecWithWater()
    {
        var spec = CreateTestSpatialSpec();
        
        var waterBodies = new List<SpatialWaterBody>
        {
            new SpatialWaterBody(
                "Main Canal",
                new List<Vector2>
                {
                    new Vector2(52.3f, 4.3f),
                    new Vector2(52.7f, 4.3f),
                    new Vector2(52.7f, 4.7f),
                    new Vector2(52.3f, 4.7f)
                },
                SpatialWaterType.Canal
            )
        };

        return new TownSpatialSpecification(
            RealWorldBounds: spec.RealWorldBounds,
            BuildingPlacements: spec.BuildingPlacements,
            RoadNetwork: spec.RoadNetwork,
            WaterBodies: waterBodies,
            TerrainDescription: spec.TerrainDescription
        );
    }
}
