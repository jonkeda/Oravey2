using System.Numerics;
using Oravey2.Contracts.Spatial;
using Xunit;
using Oravey2.MapGen.Generation;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.Tests.Generation;

public class TownMapResultSerializationTests
{
    private TownMapResult CreateBasicMapResult()
    {
        return new TownMapResult
        {
            Layout = new LayoutDto { Width = 16, Height = 16, Surface = new int[][] { new int[16] } },
            Buildings = new List<BuildingDto>(),
            Props = new List<PropDto>(),
            Zones = new List<ZoneDto>()
        };
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
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = new BoundingBox(52.0, 53.0, 4.0, 5.0),
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 10.0f },
            TerrainDescription = "flat"
        };
        var json = SpatialSpecSerializer.SerializeToJson(spec);

        // Act
        var result = new TownMapResult
        {
            Layout = new LayoutDto { Width = 16, Height = 16, Surface = new int[][] { new int[16] } },
            Buildings = new List<BuildingDto>(),
            Props = new List<PropDto>(),
            Zones = new List<ZoneDto>(),
            SpatialSpec = spec,
            SpatialSpecJson = json
        };

        // Assert
        Assert.NotNull(result.SpatialSpec);
        Assert.NotNull(result.SpatialSpecJson);
        Assert.Equal(spec, result.SpatialSpec);
    }

    [Fact]
    public void CreateWithSerializedSpec_SerializesAutomatically()
    {
        // Arrange
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = new BoundingBox(52.0, 53.0, 4.0, 5.0),
            BuildingPlacements = new Dictionary<string, BuildingPlacement> { ["Test"] = new BuildingPlacement { Name = "Test", CenterLat = 52.5, CenterLon = 4.9, WidthMeters = 20, DepthMeters = 20, RotationDegrees = 0, AlignmentHint = "test" } },
            RoadNetwork = new RoadNetwork
            {
                Nodes = new List<Vector2> { new(52.5f, 4.9f) },
                Edges = new List<RoadEdge> { new(52.5, 4.9, 52.4, 4.8) },
                RoadWidthMeters = 10.0f
            },
            WaterBodies = new List<SpatialWaterBody> { new SpatialWaterBody { Name = "Canal", Polygon = new List<Vector2> { new(52.3f, 4.7f) }, Type = SpatialWaterType.Canal } },
            TerrainDescription = "hilly"
        };

        // Act
        var result = TownMapResult.CreateWithSerializedSpec(
            new LayoutDto { Width = 16, Height = 16, Surface = new int[][] { new int[16] } },
            new List<BuildingDto>(),
            new List<PropDto>(),
            new List<ZoneDto>(),
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
            new LayoutDto { Width = 16, Height = 16, Surface = new int[][] { new int[16] } },
            new List<BuildingDto>(),
            new List<PropDto>(),
            new List<ZoneDto>(),
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
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = new BoundingBox(52.0, 53.0, 4.0, 5.0),
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 10.0f },
            TerrainDescription = "flat"
        };

        // Act
        var result = TownMapResult.CreateWithSerializedSpec(
            new LayoutDto { Width = 16, Height = 16, Surface = new int[][] { new int[16] } },
            new List<BuildingDto>(),
            new List<PropDto>(),
            new List<ZoneDto>(),
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
        var layout = new LayoutDto { Width = 16, Height = 16, Surface = new int[][] { new int[16] }, Liquid = new int[][] { new int[16] } };
        var buildings = new List<BuildingDto>
        {
            new BuildingDto { Id = "b1", Name = "Building1", MeshAsset = "mesh1", Size = "large", Footprint = new int[][] { new int[4] }, Floors = 2, Condition = 0.9f, Placement = new PlacementDto(0, 0, 5, 5) }
        };
        var props = new List<PropDto>
        {
            new PropDto { Id = "p1", MeshAsset = "mesh1", Placement = new PlacementDto(0, 0, 6, 6), Rotation = 45.0f, Scale = 1.0f, BlocksWalkability = false }
        };
        var zones = new List<ZoneDto>
        {
            new ZoneDto { Id = "z1", Name = "Zone1", Biome = 1, RadiationLevel = 0.5f, EnemyDifficultyTier = 2, IsFastTravelTarget = true, ChunkStartX = 0, ChunkStartY = 0, ChunkEndX = 7, ChunkEndY = 7 }
        };
        var spec = new TownSpatialSpecification
        {
            RealWorldBounds = new BoundingBox(52.0, 53.0, 4.0, 5.0),
            RoadNetwork = new RoadNetwork { RoadWidthMeters = 10.0f },
            TerrainDescription = "flat"
        };

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
