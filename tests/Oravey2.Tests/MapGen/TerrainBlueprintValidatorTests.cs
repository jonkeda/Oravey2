using Oravey2.Core.World.Blueprint;
using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Validation;

namespace Oravey2.Tests.MapGen;

public class TerrainBlueprintValidatorTests
{
    private static IAssetRegistry CreateTestAssets()
    {
        var catalog = new Dictionary<string, List<AssetEntry>>
        {
            ["building"] = new()
            {
                new AssetEntry("buildings/ruined_office.glb", "Office", new[] { "large" }),
                new AssetEntry("buildings/shop.glb", "Shop", new[] { "small" })
            },
            ["surface"] = new()
            {
                new AssetEntry("Asphalt", "Road", new[] { "road" }),
                new AssetEntry("Rock", "Rock", new[] { "natural" }),
                new AssetEntry("Grass", "Grass", new[] { "natural" })
            }
        };
        return new AssetRegistry(catalog);
    }

    private static TerrainBlueprintValidator CreateValidator()
        => new(CreateTestAssets());

    private static MapBlueprint ValidBlueprint() => new(
        "Test", "Test map",
        new BlueprintSource("Testville", null),
        new BlueprintDimensions(2, 2),
        new TerrainBlueprint(1,
            new[] { new TerrainRegion("flat", "flat", new[] { new[] { 0, 0 }, new[] { 31, 0 }, new[] { 31, 31 }, new[] { 0, 31 } }, 1, 1) },
            new[] { new SurfaceRule("flat", new[] { new SurfaceAllocation("Rock", 100) }) }),
        null,
        new[] { new RoadBlueprint("road1", new[] { new[] { 0, 15 }, new[] { 31, 15 } }, 2, "Asphalt", 0.9f) },
        new[] { new BuildingBlueprint("b1", "Office", "buildings/ruined_office.glb", "Large", 5, 5, 3, 3, 2, 0.7f, null) },
        null,
        new[] { new ZoneBlueprint("z1", "Downtown", "RuinedCity", 0.1f, 2, true, 0, 0, 1, 1) });

    [Fact]
    public void ValidBlueprint_NoErrors()
    {
        var validator = CreateValidator();
        var result = validator.Validate(ValidBlueprint());

        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void ZeroDimensions_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(0, 0),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, null, null, null);

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_DIMENSIONS");
    }

    [Fact]
    public void BuildingOutsideBounds_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(1, 1),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null,
            new[] { new BuildingBlueprint("b1", "Office", "buildings/ruined_office.glb", "Large", 14, 14, 4, 4, 1, 1f, null) },
            null, null);

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "BUILDING_OUT_OF_BOUNDS");
    }

    [Fact]
    public void OverlappingBuildings_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(2, 2),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null,
            new[]
            {
                new BuildingBlueprint("b1", "A", "buildings/shop.glb", "Small", 5, 5, 3, 3, 1, 1f, null),
                new BuildingBlueprint("b2", "B", "buildings/shop.glb", "Small", 6, 6, 3, 3, 1, 1f, null)
            },
            null, null);

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "BUILDING_OVERLAP");
    }

    [Fact]
    public void UnknownSurface_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(1, 1),
            new TerrainBlueprint(1,
                Array.Empty<TerrainRegion>(),
                new[] { new SurfaceRule("r1", new[] { new SurfaceAllocation("Lava", 100) }) }),
            null, null, null, null, null);

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "UNKNOWN_SURFACE");
    }

    [Fact]
    public void ZoneOutOfBounds_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(1, 1),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null, null, null,
            new[] { new ZoneBlueprint("z1", "Far", "Wasteland", 0, 1, false, 0, 0, 5, 5) });

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "ZONE_OUT_OF_BOUNDS");
    }

    [Fact]
    public void RoadOutOfBounds_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(1, 1),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null,
            new[] { new RoadBlueprint("r1", new[] { new[] { 0, 0 }, new[] { 100, 100 } }, 2, "Asphalt", 1f) },
            null, null, null);

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "ROAD_OUT_OF_BOUNDS");
    }

    [Fact]
    public void UnknownBuildingMesh_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(2, 2),
            new TerrainBlueprint(1, Array.Empty<TerrainRegion>(), Array.Empty<SurfaceRule>()),
            null, null,
            new[] { new BuildingBlueprint("b1", "X", "buildings/nonexistent.glb", "Small", 0, 0, 2, 2, 1, 1f, null) },
            null, null);

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "UNKNOWN_BUILDING_MESH");
    }

    [Fact]
    public void RegionOutOfBounds_ReturnsError()
    {
        var validator = CreateValidator();
        var bp = new MapBlueprint(
            "Test", "Test",
            new BlueprintSource("X", null),
            new BlueprintDimensions(1, 1),
            new TerrainBlueprint(1,
                new[] { new TerrainRegion("r1", "flat", new[] { new[] { 100, 100 } }, 1, 1) },
                Array.Empty<SurfaceRule>()),
            null, null, null, null, null);

        var result = validator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "REGION_OUT_OF_BOUNDS");
    }
}
