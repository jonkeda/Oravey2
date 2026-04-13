using System.Numerics;
using System.Text.Json;
using Oravey2.Contracts.Spatial;
using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class SpatialSpecSerializerTests
{
    private static TownSpatialSpecification CreateTestSpecification()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        
        var placements = new Dictionary<string, BuildingPlacement>
        {
            ["Cathedral"] = new BuildingPlacement(
                Name: "Cathedral",
                CenterLat: 52.5,
                CenterLon: 4.9,
                WidthMeters: 40.0,
                DepthMeters: 50.0,
                RotationDegrees: 45.0,
                AlignmentHint: "square_corner"
            ),
            ["Market"] = new BuildingPlacement(
                Name: "Market",
                CenterLat: 52.4,
                CenterLon: 4.8,
                WidthMeters: 20.0,
                DepthMeters: 30.0,
                RotationDegrees: 0.0,
                AlignmentHint: "on_main_road"
            )
        };

        var roadNetwork = new RoadNetwork(
            Nodes: new List<Vector2>
            {
                new Vector2(52.5f, 4.9f),
                new Vector2(52.4f, 4.8f),
                new Vector2(52.6f, 5.0f)
            },
            Edges: new List<RoadEdge>
            {
                new RoadEdge(52.5, 4.9, 52.4, 4.8),
                new RoadEdge(52.4, 4.8, 52.6, 5.0)
            },
            RoadWidthMeters: 10.0f
        );

        var waterBodies = new List<SpatialWaterBody>
        {
            new SpatialWaterBody(
                Name: "Main Canal",
                Polygon: new List<Vector2>
                {
                    new Vector2(52.3f, 4.7f),
                    new Vector2(52.7f, 4.7f),
                    new Vector2(52.7f, 5.1f),
                    new Vector2(52.3f, 5.1f)
                },
                Type: SpatialWaterType.Canal
            ),
            new SpatialWaterBody(
                Name: "Harbour",
                Polygon: new List<Vector2>
                {
                    new Vector2(52.2f, 4.6f),
                    new Vector2(52.3f, 4.6f),
                    new Vector2(52.3f, 4.7f)
                },
                Type: SpatialWaterType.Harbour
            )
        };

        return new TownSpatialSpecification(
            RealWorldBounds: bbox,
            BuildingPlacements: placements,
            RoadNetwork: roadNetwork,
            WaterBodies: waterBodies,
            TerrainDescription: "flat"
        );
    }

    [Fact]
    public void SerializeToJson_ValidSpec_ReturnsJsonString()
    {
        // Arrange
        var spec = CreateTestSpecification();

        // Act
        var json = SpatialSpecSerializer.SerializeToJson(spec);

        // Assert
        Assert.NotNull(json);
        Assert.NotEmpty(json);
        Assert.Contains("version", json);
        Assert.Contains("realWorldBounds", json);
    }

    [Fact]
    public void SerializeToJson_NullSpec_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SpatialSpecSerializer.SerializeToJson(null!));
    }

    [Fact]
    public void DeserializeFromJson_ValidJson_ReturnsSpecification()
    {
        // Arrange
        var spec = CreateTestSpecification();
        var json = SpatialSpecSerializer.SerializeToJson(spec);

        // Act
        var deserialized = SpatialSpecSerializer.DeserializeFromJson(json);

        // Assert
        Assert.NotNull(deserialized);
        Assert.Equal(spec.RealWorldBounds, deserialized.RealWorldBounds);
        Assert.Equal(spec.TerrainDescription, deserialized.TerrainDescription);
    }

    [Fact]
    public void RoundTrip_SerializeAndDeserialize_PreservesAllData()
    {
        // Arrange
        var original = CreateTestSpecification();

        // Act
        var json = SpatialSpecSerializer.SerializeToJson(original);
        var deserialized = SpatialSpecSerializer.DeserializeFromJson(json);

        // Assert - Verify all components
        Assert.Equal(original.RealWorldBounds.MinLat, deserialized.RealWorldBounds.MinLat);
        Assert.Equal(original.RealWorldBounds.MaxLat, deserialized.RealWorldBounds.MaxLat);
        Assert.Equal(original.RealWorldBounds.MinLon, deserialized.RealWorldBounds.MinLon);
        Assert.Equal(original.RealWorldBounds.MaxLon, deserialized.RealWorldBounds.MaxLon);

        Assert.Equal(original.BuildingPlacements.Count, deserialized.BuildingPlacements.Count);
        foreach (var key in original.BuildingPlacements.Keys)
        {
            var orig = original.BuildingPlacements[key];
            var deser = deserialized.BuildingPlacements[key];
            Assert.Equal(orig.Name, deser.Name);
            Assert.Equal(orig.CenterLat, deser.CenterLat);
            Assert.Equal(orig.CenterLon, deser.CenterLon);
            Assert.Equal(orig.WidthMeters, deser.WidthMeters);
            Assert.Equal(orig.DepthMeters, deser.DepthMeters);
            Assert.Equal(orig.RotationDegrees, deser.RotationDegrees);
            Assert.Equal(orig.AlignmentHint, deser.AlignmentHint);
        }

        Assert.Equal(original.RoadNetwork.Nodes.Count, deserialized.RoadNetwork.Nodes.Count);
        Assert.Equal(original.RoadNetwork.Edges.Count, deserialized.RoadNetwork.Edges.Count);
        Assert.Equal(original.RoadNetwork.RoadWidthMeters, deserialized.RoadNetwork.RoadWidthMeters);

        Assert.Equal(original.WaterBodies.Count, deserialized.WaterBodies.Count);
        Assert.Equal(original.TerrainDescription, deserialized.TerrainDescription);
    }

    [Fact]
    public void DeserializeFromJson_NullString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SpatialSpecSerializer.DeserializeFromJson(null!));
    }

    [Fact]
    public void DeserializeFromJson_EmptyString_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => SpatialSpecSerializer.DeserializeFromJson(""));
    }

    [Fact]
    public void DeserializeFromJson_InvalidJson_ThrowsJsonException()
    {
        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => SpatialSpecSerializer.DeserializeFromJson("not json at all"));
        Assert.NotNull(ex.Message);
    }

    [Fact]
    public void DeserializeFromJson_MissingRealWorldBounds_ThrowsJsonException()
    {
        var json = @"{ ""version"": 1, ""buildingPlacements"": {}, ""roadNetwork"": null }";
        
        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => SpatialSpecSerializer.DeserializeFromJson(json));
        Assert.Contains("RealWorldBounds", ex.Message);
    }

    [Fact]
    public void DeserializeFromJson_MissingRoadNetwork_ThrowsJsonException()
    {
        var json = @"{ ""version"": 1, ""realWorldBounds"": { ""minLat"": 52, ""maxLat"": 53, ""minLon"": 4, ""maxLon"": 5 }, ""roadNetwork"": null }";
        
        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => SpatialSpecSerializer.DeserializeFromJson(json));
        Assert.Contains("RoadNetwork", ex.Message);
    }

    [Fact]
    public void DeserializeFromJson_UnsupportedVersion_ThrowsJsonException()
    {
        var json = @"{ ""version"": 99 }";
        
        // Act & Assert
        var ex = Assert.Throws<JsonException>(() => SpatialSpecSerializer.DeserializeFromJson(json));
        Assert.Contains("version", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeAndDeserialize_EmptyBuildingsAndWaters_Success()
    {
        // Arrange
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var network = new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f);
        var spec = new TownSpatialSpecification(bbox, new Dictionary<string, BuildingPlacement>(), network, new List<SpatialWaterBody>(), "flat");

        // Act
        var json = SpatialSpecSerializer.SerializeToJson(spec);
        var deserialized = SpatialSpecSerializer.DeserializeFromJson(json);

        // Assert
        Assert.Empty(deserialized.BuildingPlacements);
        Assert.Empty(deserialized.WaterBodies);
        Assert.Empty(deserialized.RoadNetwork.Nodes);
    }

    [Fact]
    public void SerializeAndDeserialize_AllWaterTypes_Success()
    {
        // Arrange
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var network = new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f);
        var waters = new List<SpatialWaterBody>
        {
            new("River", new List<Vector2> { new(52.5f, 4.9f) }, SpatialWaterType.River),
            new("Canal", new List<Vector2> { new(52.4f, 4.8f) }, SpatialWaterType.Canal),
            new("Harbour", new List<Vector2> { new(52.3f, 4.7f) }, SpatialWaterType.Harbour),
            new("Lake", new List<Vector2> { new(52.2f, 4.6f) }, SpatialWaterType.Lake)
        };
        var spec = new TownSpatialSpecification(bbox, new Dictionary<string, BuildingPlacement>(), network, waters, "hilly");

        // Act
        var json = SpatialSpecSerializer.SerializeToJson(spec);
        var deserialized = SpatialSpecSerializer.DeserializeFromJson(json);

        // Assert
        Assert.Equal(4, deserialized.WaterBodies.Count);
        Assert.Equal(SpatialWaterType.River, deserialized.WaterBodies[0].Type);
        Assert.Equal(SpatialWaterType.Canal, deserialized.WaterBodies[1].Type);
        Assert.Equal(SpatialWaterType.Harbour, deserialized.WaterBodies[2].Type);
        Assert.Equal(SpatialWaterType.Lake, deserialized.WaterBodies[3].Type);
    }

    [Fact]
    public void SerializeAndDeserialize_LargeSpec_Success()
    {
        // Arrange - Create spec with many buildings and road segments
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var placements = new Dictionary<string, BuildingPlacement>();
        var nodes = new List<Vector2>();
        var edges = new List<RoadEdge>();

        for (int i = 0; i < 100; i++)
        {
            placements[$"Building{i}"] = new BuildingPlacement(
                Name: $"Building{i}",
                CenterLat: 52.0 + (i * 0.001),
                CenterLon: 4.0 + (i * 0.001),
                WidthMeters: 20.0 + i,
                DepthMeters: 30.0 + i,
                RotationDegrees: (i * 3.6) % 360,
                AlignmentHint: "residential"
            );
        }

        for (int i = 0; i < 50; i++)
        {
            nodes.Add(new Vector2(52.0f + i * 0.01f, 4.0f + i * 0.01f));
        }

        for (int i = 0; i < nodes.Count - 1; i++)
        {
            edges.Add(new RoadEdge(nodes[i].X, nodes[i].Y, nodes[i + 1].X, nodes[i + 1].Y));
        }

        var network = new RoadNetwork(nodes, edges, 10.0f);
        var spec = new TownSpatialSpecification(bbox, placements, network, new List<SpatialWaterBody>(), "sloped south-west");

        // Act
        var json = SpatialSpecSerializer.SerializeToJson(spec);
        var deserialized = SpatialSpecSerializer.DeserializeFromJson(json);

        // Assert
        Assert.Equal(100, deserialized.BuildingPlacements.Count);
        Assert.Equal(50, deserialized.RoadNetwork.Nodes.Count);
        Assert.Equal(49, deserialized.RoadNetwork.Edges.Count);
        Assert.Equal("sloped south-west", deserialized.TerrainDescription);
    }

    [Fact]
    public void SerializeToJson_ContainsCamelCasePropertyNames()
    {
        // Arrange
        var spec = CreateTestSpecification();

        // Act
        var json = SpatialSpecSerializer.SerializeToJson(spec);

        // Assert - Verify camelCase naming
        Assert.Contains("\"version\"", json);
        Assert.Contains("\"realWorldBounds\"", json);
        Assert.Contains("\"buildingPlacements\"", json);
        Assert.Contains("\"roadNetwork\"", json);
        Assert.Contains("\"waterBodies\"", json);
        Assert.Contains("\"terrainDescription\"", json);
    }

    [Fact]
    public void RoundTrip_MultipleSpecs_AllPreserved()
    {
        // Arrange
        var specs = new[]
        {
            new TownSpatialSpecification(
                new BoundingBox(52.0, 53.0, 4.0, 5.0),
                new Dictionary<string, BuildingPlacement>(),
                new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f),
                new List<SpatialWaterBody>(),
                "flat"
            ),
            new TownSpatialSpecification(
                new BoundingBox(51.0, 52.0, 3.0, 4.0),
                new Dictionary<string, BuildingPlacement> { ["Test"] = new("Test", 51.5, 3.5, 20, 20, 0, "test") },
                new RoadNetwork(new List<Vector2> { new(51.5f, 3.5f) }, new List<RoadEdge>(), 10.0f),
                new List<SpatialWaterBody> { new("Test", new List<Vector2> { new(51.4f, 3.4f) }, SpatialWaterType.River) },
                "hilly"
            )
        };

        // Act & Assert
        foreach (var spec in specs)
        {
            var json = SpatialSpecSerializer.SerializeToJson(spec);
            var deserialized = SpatialSpecSerializer.DeserializeFromJson(json);
            Assert.Equal(spec.TerrainDescription, deserialized.TerrainDescription);
        }
    }
}
