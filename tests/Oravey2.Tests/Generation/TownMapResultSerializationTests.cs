using System.Numerics;
using Oravey2.Contracts.Spatial;
using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class TownMapResultSerializationTests
{
    private TownMapResult CreateBasicMapResult()
    {
        return new TownMapResult(
            Layout: new TownLayout(Width: 16, Height: 16, Surface: new int[][] { new int[16] }),
            Buildings: new List<PlacedBuilding>(),
            Props: new List<PlacedProp>(),
            Zones: new List<TownZone>()
        );
    }

    [Fact]
    public void TownMapResult_WithoutSpatialSpec_HasNullJsonField()
    {
        // Arrange & Act
        var result = CreateBasicMapResult();

        // Assert
        Assert.Null(result.SpatialSpec);
        Assert.Null(result.SpatialSpecJson);
    }

    [Fact]
    public void TownMapResult_WithSpatialSpec_CanStoreJson()
    {
        // Arrange
        var spec = new TownSpatialSpecification(
            new BoundingBox(52.0, 53.0, 4.0, 5.0),
            new Dictionary<string, BuildingPlacement>(),
            new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f),
            new List<SpatialWaterBody>(),
            "flat"
        );
        var json = SpatialSpecSerializer.SerializeToJson(spec);

        // Act
        var result = new TownMapResult(
            Layout: new TownLayout(Width: 16, Height: 16, Surface: new int[][] { new int[16] }),
            Buildings: new List<PlacedBuilding>(),
            Props: new List<PlacedProp>(),
            Zones: new List<TownZone>(),
            SpatialSpec: spec,
            SpatialSpecJson: json
        );

        // Assert
        Assert.NotNull(result.SpatialSpec);
        Assert.NotNull(result.SpatialSpecJson);
        Assert.Equal(spec, result.SpatialSpec);
    }

    [Fact]
    public void CreateWithSerializedSpec_SerializesAutomatically()
    {
        // Arrange
        var spec = new TownSpatialSpecification(
            new BoundingBox(52.0, 53.0, 4.0, 5.0),
            new Dictionary<string, BuildingPlacement> { ["Test"] = new("Test", 52.5, 4.9, 20, 20, 0, "test") },
            new RoadNetwork(
                new List<Vector2> { new(52.5f, 4.9f) },
                new List<RoadEdge> { new(52.5, 4.9, 52.4, 4.8) },
                10.0f
            ),
            new List<SpatialWaterBody> { new("Canal", new List<Vector2> { new(52.3f, 4.7f) }, SpatialWaterType.Canal) },
            "hilly"
        );

        // Act
        var result = TownMapResult.CreateWithSerializedSpec(
            new TownLayout(Width: 16, Height: 16, Surface: new int[][] { new int[16] }),
            new List<PlacedBuilding>(),
            new List<PlacedProp>(),
            new List<TownZone>(),
            spec
        );

        // Assert
        Assert.NotNull(result.SpatialSpec);
        Assert.NotNull(result.SpatialSpecJson);
        Assert.Contains("\"version\"", result.SpatialSpecJson);
        Assert.Contains("\"realWorldBounds\"", result.SpatialSpecJson);
    }

    [Fact]
    public void CreateWithSerializedSpec_WithoutSpec_HasNullJson()
    {
        // Act
        var result = TownMapResult.CreateWithSerializedSpec(
            new TownLayout(Width: 16, Height: 16, Surface: new int[][] { new int[16] }),
            new List<PlacedBuilding>(),
            new List<PlacedProp>(),
            new List<TownZone>(),
            null
        );

        // Assert
        Assert.Null(result.SpatialSpec);
        Assert.Null(result.SpatialSpecJson);
    }

    [Fact]
    public void TownMapResult_JsonFieldCanBeDeserialized()
    {
        // Arrange
        var spec = new TownSpatialSpecification(
            new BoundingBox(52.0, 53.0, 4.0, 5.0),
            new Dictionary<string, BuildingPlacement>(),
            new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f),
            new List<SpatialWaterBody>(),
            "flat"
        );

        // Act
        var result = TownMapResult.CreateWithSerializedSpec(
            new TownLayout(Width: 16, Height: 16, Surface: new int[][] { new int[16] }),
            new List<PlacedBuilding>(),
            new List<PlacedProp>(),
            new List<TownZone>(),
            spec
        );

        var deserialized = SpatialSpecSerializer.DeserializeFromJson(result.SpatialSpecJson!);

        // Assert
        Assert.Equal(spec.RealWorldBounds, deserialized.RealWorldBounds);
        Assert.Equal(spec.TerrainDescription, deserialized.TerrainDescription);
    }

    [Fact]
    public void CreateWithSerializedSpec_PreservesAllMapData()
    {
        // Arrange
        var layout = new TownLayout(Width: 16, Height: 16, Surface: new int[][] { new int[16] }, Liquid: new int[][] { new int[16] });
        var buildings = new List<PlacedBuilding>
        {
            new("b1", "Building1", "mesh1", "large", new int[][] { new int[4] }, 2, 0.9f, new TilePlacement(0, 0, 5, 5))
        };
        var props = new List<PlacedProp>
        {
            new("p1", "mesh1", new TilePlacement(0, 0, 6, 6), 45.0f, 1.0f, false)
        };
        var zones = new List<TownZone>
        {
            new("z1", "Zone1", 1, 0.5f, 2, true, 0, 0, 7, 7)
        };
        var spec = new TownSpatialSpecification(
            new BoundingBox(52.0, 53.0, 4.0, 5.0),
            new Dictionary<string, BuildingPlacement>(),
            new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f),
            new List<SpatialWaterBody>(),
            "flat"
        );

        // Act
        var result = TownMapResult.CreateWithSerializedSpec(layout, buildings, props, zones, spec);

        // Assert
        Assert.Equal(layout, result.Layout);
        Assert.Equal(buildings, result.Buildings);
        Assert.Equal(props, result.Props);
        Assert.Equal(zones, result.Zones);
        Assert.Equal(spec, result.SpatialSpec);
        Assert.NotNull(result.SpatialSpecJson);
    }
}
