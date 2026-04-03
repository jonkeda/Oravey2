using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class MapBlueprintTests
{
    [Fact]
    public void MinimalBlueprint_Instantiates()
    {
        var bp = TestBlueprints.Minimal();
        Assert.Equal("Test", bp.Name);
        Assert.Equal(1, bp.Dimensions.ChunksWide);
        Assert.Equal(1, bp.Dimensions.ChunksHigh);
    }

    [Fact]
    public void MinimalBlueprint_SerializeDeserialize_RoundTrip()
    {
        var bp = TestBlueprints.Minimal();
        var json = System.Text.Json.JsonSerializer.Serialize(bp, BlueprintLoader.WriteOptions);
        var restored = BlueprintLoader.LoadFromString(json);

        Assert.Equal(bp.Name, restored.Name);
        Assert.Equal(bp.Dimensions.ChunksWide, restored.Dimensions.ChunksWide);
        Assert.Equal(bp.Terrain.BaseElevation, restored.Terrain.BaseElevation);
    }

    [Fact]
    public void FullBlueprint_AllFieldsPopulated()
    {
        var bp = TestBlueprints.Full2x2();
        Assert.NotNull(bp.Water);
        Assert.NotNull(bp.Roads);
        Assert.NotNull(bp.Buildings);
        Assert.NotNull(bp.Props);
        Assert.NotNull(bp.Zones);
    }
}

/// <summary>
/// Shared test blueprint factories.
/// </summary>
internal static class TestBlueprints
{
    public static MapBlueprint Minimal() => new(
        "Test", "Test map",
        new BlueprintSource("Testville", null),
        new BlueprintDimensions(1, 1),
        new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
        null, null, null, null, null);

    public static MapBlueprint Full2x2() => new(
        "Portland", "Small Portland map",
        new BlueprintSource("Portland, OR", "Test fixture"),
        new BlueprintDimensions(2, 2),
        new TerrainBlueprint(3,
            new[]
            {
                new TerrainRegion("hill", "elevation",
                    new[] { new[] {20,20}, new[] {28,20}, new[] {28,28}, new[] {20,28} }, 5, 8)
            },
            new[]
            {
                new SurfaceRule("hill", new[] { new SurfaceAllocation("Rock", 100) })
            }),
        new WaterBlueprint(
            new[]
            {
                new RiverBlueprint("river1", new[] { new[] {5,0}, new[] {5,31} }, 3, 4, null)
            },
            new[]
            {
                new LakeBlueprint("lake1", 25, 10, 3, 5, 3)
            }),
        new[]
        {
            new RoadBlueprint("road1", new[] { new[] {0,15}, new[] {31,15} }, 2, "Asphalt", 0.9f),
            new RoadBlueprint("road2", new[] { new[] {15,0}, new[] {15,31} }, 2, "Concrete", 1.0f)
        },
        new[]
        {
            new BuildingBlueprint("shop1", "Corner Shop", "meshes/shop.glb",
                "Small", 10, 10, 2, 2, 1, 0.8f, null)
        },
        new[]
        {
            new PropBlueprint("car1", "meshes/car.glb", 8, 8, 45f, 1f, true, 2, 1),
            new PropBlueprint("barrel1", "meshes/barrel.glb", 12, 5, 0f, 1f, false, 0, 0)
        },
        new[]
        {
            new ZoneBlueprint("downtown", "Downtown", "RuinedCity", 0.1f, 2, true, 0, 0, 0, 0),
            new ZoneBlueprint("outskirts", "Outskirts", "Wasteland", 0.3f, 3, false, 1, 0, 1, 1)
        });
}
