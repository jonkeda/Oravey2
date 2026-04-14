using System.Numerics;
using Oravey2.Contracts.Spatial;
using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class TownSpatialSpecificationTests
{
    [Fact]
    public void BoundingBox_Equality_TwoIdentical()
    {
        var bbox1 = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var bbox2 = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        
        Assert.Equal(bbox1, bbox2);
    }

    [Fact]
    public void TownSpatialSpecification_Created_WithAllComponents()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        
        var placements = new Dictionary<string, BuildingPlacement>
        {
            ["Cathedral"] = new BuildingPlacement { Name = "Cathedral", CenterLat = 52.5, CenterLon = 4.9, WidthMeters = 40, DepthMeters = 50, RotationDegrees = 45, AlignmentHint = "square_corner" },
            ["Market"] = new BuildingPlacement { Name = "Market", CenterLat = 52.4, CenterLon = 4.8, WidthMeters = 20, DepthMeters = 30, RotationDegrees = 0, AlignmentHint = "on_main_road" }
        };

        var network = new RoadNetwork
        {
            Nodes = new List<Vector2> { new Vector2(52.5f, 4.9f), new Vector2(52.4f, 4.8f) },
            Edges = new List<RoadEdge> { new RoadEdge(52.5, 4.9, 52.4, 4.8) },
            RoadWidthMeters = 10.0f
        };

        var waters = new List<SpatialWaterBody>
        {
            new SpatialWaterBody
            {
                Name = "Main Canal",
                Polygon = new List<Vector2> 
                { 
                    new Vector2(52.3f, 4.7f), 
                    new Vector2(52.7f, 4.7f),
                    new Vector2(52.7f, 5.1f)
                },
                Type = SpatialWaterType.Canal
            }
        };

        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = bbox,
            BuildingPlacements = placements,
            RoadNetwork = network,
            WaterBodies = waters,
            TerrainDescription = "flat"
        };

        Assert.NotNull(spec);
        Assert.Equal(2, spec.BuildingPlacements.Count);
        Assert.Single(spec.RoadNetwork.Edges);
        Assert.Single(spec.WaterBodies);
        Assert.Equal("flat", spec.TerrainDescription);
    }

    [Fact]
    public void TownSpatialSpecification_EmptyCollections_Valid()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var network = new RoadNetwork { RoadWidthMeters = 10.0f };

        var spec = new TownSpatialSpecification { RealWorldBounds = bbox, RoadNetwork = network, TerrainDescription = "flat" };

        Assert.Empty(spec.BuildingPlacements);
        Assert.Empty(spec.WaterBodies);
    }

    [Fact]
    public void TownDesign_IncludesSpatialSpec_Nullable()
    {
        var design = new TownDesign(
            TownName: "TestTown",
            Landmarks: [],
            KeyLocations: [],
            LayoutStyle: "grid",
            Hazards: [],
            SpatialSpec: null
        );

        Assert.Null(design.SpatialSpec);
    }

    [Fact]
    public void TownDesign_IncludesSpatialSpec_WithData()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var network = new RoadNetwork { RoadWidthMeters = 10.0f };
        var spec = new TownSpatialSpecification { RealWorldBounds = bbox, RoadNetwork = network, TerrainDescription = "flat" };

        var design = new TownDesign(
            TownName: "TestTown",
            Landmarks: [],
            KeyLocations: [],
            LayoutStyle: "grid",
            Hazards: [],
            SpatialSpec: spec
        );

        Assert.NotNull(design.SpatialSpec);
        Assert.Equal(52.0, design.SpatialSpec.RealWorldBounds.MinLat);
    }

    [Fact]
    public void BoundingBox_LatLonRange_Valid()
    {
        var bbox = new BoundingBox(
            MinLat: 52.0,
            MaxLat: 52.5,    // 0.5 degree
            MinLon: 4.0,
            MaxLon: 4.5       // 0.5 degree
        );

        Assert.True(bbox.MaxLat > bbox.MinLat);
        Assert.True(bbox.MaxLon > bbox.MinLon);
    }
}
