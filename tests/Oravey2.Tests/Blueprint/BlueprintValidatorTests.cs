using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class BlueprintValidatorTests
{
    [Fact]
    public void ValidBlueprint_IsValid()
    {
        var bp = TestBlueprints.Full2x2();
        var result = BlueprintValidator.Validate(bp);
        Assert.True(result.IsValid, string.Join("; ", result.Errors.Select(e => e.Message)));
    }

    [Fact]
    public void ZeroDimensions_Error()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Dimensions = new BlueprintDimensions(0, 1)
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_DIMENSIONS");
    }

    [Fact]
    public void NegativeDimensions_Error()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Dimensions = new BlueprintDimensions(-1, -1)
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.Contains(result.Errors, e => e.Code == "INVALID_DIMENSIONS");
    }

    [Fact]
    public void RegionOutsideBounds_Error()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Terrain = new TerrainBlueprint(1,
                new[]
                {
                    new TerrainRegion("oob", "elevation",
                        new[] { new[] {0,0}, new[] {100,0}, new[] {100,100} }, 1, 5)
                },
                Array.Empty<SurfaceRule>())
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.Contains(result.Errors, e => e.Code == "REGION_OUT_OF_BOUNDS");
    }

    [Fact]
    public void OverlappingBuildings_Error()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Buildings = new[]
            {
                new BuildingBlueprint("b1", "B1", "meshes/b1.glb", "Small", 2, 2, 2, 2, 1, 1f, null),
                new BuildingBlueprint("b2", "B2", "meshes/b2.glb", "Small", 3, 2, 2, 2, 1, 1f, null)
                // b1 covers (2,2)(3,2)(2,3)(3,3) and b2 covers (3,2)(4,2)(3,3)(4,3) → overlap at (3,2),(3,3)
            }
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.Contains(result.Errors, e => e.Code == "BUILDING_OVERLAP");
    }

    [Fact]
    public void ZoneOutsideDimensions_Error()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Zones = new[]
            {
                new ZoneBlueprint("oob", "OOB", "Wasteland", 0, 1, false, 0, 0, 5, 5)
            }
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.Contains(result.Errors, e => e.Code == "ZONE_OUT_OF_BOUNDS");
    }

    [Fact]
    public void EmptyMeshAsset_Error()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Buildings = new[]
            {
                new BuildingBlueprint("b1", "B1", "", "Small", 2, 2, 2, 2, 1, 1f, null)
            }
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.Contains(result.Errors, e => e.Code == "MISSING_MESH_ASSET");
    }

    [Fact]
    public void MultipleErrors_AllReported()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Dimensions = new BlueprintDimensions(0, 0),
            Buildings = new[]
            {
                new BuildingBlueprint("b1", "B1", "", "Small", 2, 2, 1, 1, 1, 1f, null)
            }
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.True(result.Errors.Length >= 2);
    }

    [Fact]
    public void RoadOutsideBounds_Error()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Roads = new[]
            {
                new RoadBlueprint("r1", new[] { new[] {0,0}, new[] {100,0} }, 2, "Asphalt", 1f)
            }
        };
        var result = BlueprintValidator.Validate(bp);
        Assert.Contains(result.Errors, e => e.Code == "ROAD_OUT_OF_BOUNDS");
    }

    [Fact]
    public void MinimalValid_NoErrors()
    {
        var bp = TestBlueprints.Minimal();
        var result = BlueprintValidator.Validate(bp);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }
}
