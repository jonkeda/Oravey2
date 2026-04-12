using Xunit;
using Oravey2.MapGen.Generation;
using System.Text.Json;

namespace Oravey2.Tests.Generation;

public class LlmTownSpatialSpecTests
{
    [Fact]
    public void LlmBoundingBoxDto_Created_WithValidCoordinates()
    {
        var bbox = new LlmBoundingBoxDto
        {
            MinLat = 52.0,
            MaxLat = 53.0,
            MinLon = 4.0,
            MaxLon = 5.0
        };

        Assert.Equal(52.0, bbox.MinLat);
        Assert.Equal(53.0, bbox.MaxLat);
        Assert.Equal(4.0, bbox.MinLon);
        Assert.Equal(5.0, bbox.MaxLon);
    }

    [Fact]
    public void LlmCoordinateDto_Created()
    {
        var coord = new LlmCoordinateDto { Lat = 52.5, Lon = 4.9 };
        Assert.Equal(52.5, coord.Lat);
        Assert.Equal(4.9, coord.Lon);
    }

    [Fact]
    public void LlmBuildingPlacementDto_Created_WithAllFields()
    {
        var placement = new LlmBuildingPlacementDto
        {
            BuildingName = "Cathedral",
            CenterLat = 52.5,
            CenterLon = 4.9,
            WidthMeters = 40,
            DepthMeters = 50,
            RotationDegrees = 45,
            AlignmentHint = "square_corner",
            Notes = "Historic church"
        };

        Assert.Equal("Cathedral", placement.BuildingName);
        Assert.Equal(52.5, placement.CenterLat);
        Assert.Equal(45.0, placement.RotationDegrees);
        Assert.Equal("Historic church", placement.Notes);
    }

    [Fact]
    public void LlmRoadEdgeDto_Created()
    {
        var edge = new LlmRoadEdgeDto
        {
            FromNodeIndex = 0,
            ToNodeIndex = 1,
            RoadType = "main"
        };

        Assert.Equal(0, edge.FromNodeIndex);
        Assert.Equal(1, edge.ToNodeIndex);
        Assert.Equal("main", edge.RoadType);
    }

    [Fact]
    public void LlmRoadNetworkDto_Created_WithNodesAndEdges()
    {
        var network = new LlmRoadNetworkDto
        {
            Nodes = new List<LlmCoordinateDto>
            {
                new() { Lat = 52.5, Lon = 4.9 },
                new() { Lat = 52.4, Lon = 4.8 }
            },
            Edges = new List<LlmRoadEdgeDto>
            {
                new() { FromNodeIndex = 0, ToNodeIndex = 1, RoadType = "main" }
            },
            RoadWidthMeters = 15.0f
        };

        Assert.Equal(2, network.Nodes.Count);
        Assert.Single(network.Edges);
        Assert.Equal(15.0f, network.RoadWidthMeters);
    }

    [Fact]
    public void LlmWaterBodyDto_Created()
    {
        var water = new LlmWaterBodyDto
        {
            Name = "Main Canal",
            Type = "canal",
            Polygon = new List<LlmCoordinateDto>
            {
                new() { Lat = 52.3, Lon = 4.7 },
                new() { Lat = 52.7, Lon = 4.7 },
                new() { Lat = 52.7, Lon = 5.1 }
            }
        };

        Assert.Equal("Main Canal", water.Name);
        Assert.Equal("canal", water.Type);
        Assert.Equal(3, water.Polygon.Count);
    }

    [Fact]
    public void LlmTownSpatialSpec_Created_WithAllComponents()
    {
        var spec = new LlmTownSpatialSpec
        {
            RealWorldBounds = new LlmBoundingBoxDto
            {
                MinLat = 52.0,
                MaxLat = 53.0,
                MinLon = 4.0,
                MaxLon = 5.0
            },
            BuildingPlacements = new List<LlmBuildingPlacementDto>
            {
                new() {
                    BuildingName = "Cathedral",
                    CenterLat = 52.5,
                    CenterLon = 4.9,
                    WidthMeters = 40,
                    DepthMeters = 50,
                    RotationDegrees = 45,
                    AlignmentHint = "square_corner"
                }
            },
            RoadNetwork = new LlmRoadNetworkDto
            {
                Nodes = [new() { Lat = 52.5, Lon = 4.9 }],
                Edges = [],
                RoadWidthMeters = 10.0f
            },
            WaterBodies = [new() { Name = "Canal", Type = "canal", Polygon = [] }],
            TerrainDescription = "flat"
        };

        Assert.NotNull(spec);
        Assert.Single(spec.BuildingPlacements);
        Assert.Single(spec.WaterBodies);
        Assert.Equal("flat", spec.TerrainDescription);
    }

    [Fact]
    public void LlmTownDesignEntry_IncludesSpatialSpec()
    {
        var entry = new LlmTownDesignEntry
        {
            Landmarks = [],
            KeyLocations = [],
            LayoutStyle = "grid",
            Hazards = [],
            SpatialSpec = new LlmTownSpatialSpec
            {
                RealWorldBounds = new LlmBoundingBoxDto { MinLat = 52.0, MaxLat = 53.0, MinLon = 4.0, MaxLon = 5.0 },
                TerrainDescription = "flat"
            }
        };

        Assert.NotNull(entry.SpatialSpec);
        Assert.Equal(52.0, entry.SpatialSpec.RealWorldBounds.MinLat);
    }

    [Fact]
    public void LlmTownDesignEntry_SerializesToJson()
    {
        var entry = new LlmTownDesignEntry
        {
            SpatialSpec = new LlmTownSpatialSpec
            {
                RealWorldBounds = new LlmBoundingBoxDto
                {
                    MinLat = 52.0,
                    MaxLat = 53.0,
                    MinLon = 4.0,
                    MaxLon = 5.0
                }
            }
        };

        var json = JsonSerializer.Serialize(entry, new JsonSerializerOptions { WriteIndented = false, PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Assert.Contains("spatialSpec", json);
        Assert.Contains("realWorldBounds", json);
        Assert.Contains("52", json);
    }

    [Fact]
    public void BuildingPlacementDto_VariousAlignments()
    {
        var hints = new[] { "on_main_road", "square_corner", "harbour_adjacent", "residential_area", "hillside" };

        foreach (var hint in hints)
        {
            var placement = new LlmBuildingPlacementDto
            {
                BuildingName = "Test",
                CenterLat = 52.5,
                CenterLon = 4.9,
                WidthMeters = 20,
                DepthMeters = 20,
                RotationDegrees = 0,
                AlignmentHint = hint
            };

            Assert.Equal(hint, placement.AlignmentHint);
        }
    }

    [Fact]
    public void BuildingPlacementDto_Rotation_VariousValues()
    {
        for (int rotation = 0; rotation <= 360; rotation += 45)
        {
            var placement = new LlmBuildingPlacementDto
            {
                BuildingName = "Test",
                CenterLat = 52.5,
                CenterLon = 4.9,
                WidthMeters = 20,
                DepthMeters = 20,
                RotationDegrees = rotation
            };

            Assert.Equal(rotation, (int)placement.RotationDegrees);
        }
    }
}
