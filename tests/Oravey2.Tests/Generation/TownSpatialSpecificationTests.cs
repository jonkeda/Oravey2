using System.Numerics;
using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class TownSpatialSpecificationTests
{
    [Fact]
    public void BoundingBox_Created_WithValidCoordinates()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        
        Assert.Equal(52.0, bbox.MinLat);
        Assert.Equal(53.0, bbox.MaxLat);
        Assert.Equal(4.0, bbox.MinLon);
        Assert.Equal(5.0, bbox.MaxLon);
    }

    [Fact]
    public void BoundingBox_Equality_TwoIdentical()
    {
        var bbox1 = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var bbox2 = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        
        Assert.Equal(bbox1, bbox2);
    }

    [Fact]
    public void BuildingPlacement_Created_WithValidData()
    {
        var placement = new BuildingPlacement(
            Name: "Cathedral",
            CenterLat: 52.5,
            CenterLon: 4.9,
            WidthMeters: 40.0,
            DepthMeters: 50.0,
            RotationDegrees: 45.0,
            AlignmentHint: "square_corner"
        );

        Assert.Equal("Cathedral", placement.Name);
        Assert.Equal(52.5, placement.CenterLat);
        Assert.Equal(4.9, placement.CenterLon);
        Assert.Equal(40.0, placement.WidthMeters);
        Assert.Equal(50.0, placement.DepthMeters);
        Assert.Equal(45.0, placement.RotationDegrees);
        Assert.Equal("square_corner", placement.AlignmentHint);
    }

    [Fact]
    public void BuildingPlacement_VariousAlignments()
    {
        var hints = new[] { "on_main_road", "square_corner", "harbour_adjacent", "residential_area", "hillside" };

        foreach (var hint in hints)
        {
            var placement = new BuildingPlacement("Test", 52.5, 4.9, 20, 20, 0, hint);
            Assert.Equal(hint, placement.AlignmentHint);
        }
    }

    [Fact]
    public void RoadEdge_Created_WithCoordinates()
    {
        var edge = new RoadEdge(
            FromLat: 52.5,
            FromLon: 4.9,
            ToLat: 52.4,
            ToLon: 4.8
        );

        Assert.Equal(52.5, edge.FromLat);
        Assert.Equal(4.9, edge.FromLon);
        Assert.Equal(52.4, edge.ToLat);
        Assert.Equal(4.8, edge.ToLon);
    }

    [Fact]
    public void RoadNetwork_Created_WithNodesAndEdges()
    {
        var nodes = new List<Vector2>
        {
            new Vector2(52.5f, 4.9f),
            new Vector2(52.4f, 4.8f),
            new Vector2(52.6f, 5.0f)
        };

        var edges = new List<RoadEdge>
        {
            new RoadEdge(52.5, 4.9, 52.4, 4.8),
            new RoadEdge(52.4, 4.8, 52.6, 5.0)
        };

        var network = new RoadNetwork(nodes, edges, RoadWidthMeters: 10.0f);

        Assert.Equal(3, network.Nodes.Count);
        Assert.Equal(2, network.Edges.Count);
        Assert.Equal(10.0f, network.RoadWidthMeters);
    }

    [Fact]
    public void WaterBody_Created_WithPolygon()
    {
        var polygon = new List<Vector2>
        {
            new Vector2(52.5f, 4.9f),
            new Vector2(52.6f, 4.9f),
            new Vector2(52.6f, 5.0f),
            new Vector2(52.5f, 5.0f)
        };

        var water = new SpatialWaterBody(
            Name: "Main Canal",
            Polygon: polygon,
            Type: SpatialWaterType.Canal
        );

        Assert.Equal("Main Canal", water.Name);
        Assert.Equal(4, water.Polygon.Count);
        Assert.Equal(SpatialWaterType.Canal, water.Type);
    }

    [Fact]
    public void WaterType_AllValues()
    {
        var types = new[] { SpatialWaterType.River, SpatialWaterType.Canal, SpatialWaterType.Harbour, SpatialWaterType.Lake };

        foreach (var type in types)
        {
            Assert.True(Enum.IsDefined(typeof(SpatialWaterType), type));
        }
    }

    [Fact]
    public void TownSpatialSpecification_Created_WithAllComponents()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        
        var placements = new Dictionary<string, BuildingPlacement>
        {
            ["Cathedral"] = new BuildingPlacement("Cathedral", 52.5, 4.9, 40, 50, 45, "square_corner"),
            ["Market"] = new BuildingPlacement("Market", 52.4, 4.8, 20, 30, 0, "on_main_road")
        };

        var network = new RoadNetwork(
            Nodes: new List<Vector2> { new Vector2(52.5f, 4.9f), new Vector2(52.4f, 4.8f) },
            Edges: new List<RoadEdge> { new RoadEdge(52.5, 4.9, 52.4, 4.8) },
            RoadWidthMeters: 10.0f
        );

        var waters = new List<SpatialWaterBody>
        {
            new SpatialWaterBody("Main Canal", 
                new List<Vector2> 
                { 
                    new Vector2(52.3f, 4.7f), 
                    new Vector2(52.7f, 4.7f),
                    new Vector2(52.7f, 5.1f)
                }, 
                SpatialWaterType.Canal)
        };

        var spec = new TownSpatialSpecification(
            RealWorldBounds: bbox,
            BuildingPlacements: placements,
            RoadNetwork: network,
            WaterBodies: waters,
            TerrainDescription: "flat"
        );

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
        var network = new RoadNetwork([], [], 10.0f);

        var spec = new TownSpatialSpecification(bbox, [], network, [], "flat");

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
        var network = new RoadNetwork([], [], 10.0f);
        var spec = new TownSpatialSpecification(bbox, [], network, [], "flat");

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
    public void BuildingPlacement_Rotation_0To360()
    {
        for (int rotation = 0; rotation <= 360; rotation += 45)
        {
            var placement = new BuildingPlacement("Test", 52.5, 4.9, 20, 20, rotation, "test");
            Assert.Equal(rotation, (int)placement.RotationDegrees);
        }
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
