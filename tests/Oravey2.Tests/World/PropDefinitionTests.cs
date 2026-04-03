using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class PropDefinitionTests
{
    [Fact]
    public void NonBlockingProp_FootprintNull()
    {
        var prop = new PropDefinition(
            "barrel_001", "meshes/barrel.glb",
            0, 0, 5, 5, 0f, 1.0f, false, null);

        Assert.False(prop.BlocksWalkability);
        Assert.Null(prop.Footprint);
    }

    [Fact]
    public void BlockingProp_HasFootprint()
    {
        var prop = new PropDefinition(
            "car_wreck_001", "meshes/car_wreck.glb",
            0, 0, 3, 7, 45f, 1.0f, true,
            new[] { (3, 7), (4, 7) });

        Assert.True(prop.BlocksWalkability);
        Assert.NotNull(prop.Footprint);
        Assert.Equal(2, prop.Footprint!.Length);
    }

    [Fact]
    public void Scale_DefaultsTo1()
    {
        var prop = new PropDefinition(
            "crate_001", "meshes/crate.glb",
            0, 0, 1, 1, 0f, 1.0f, false, null);

        Assert.Equal(1.0f, prop.Scale);
    }

    [Fact]
    public void Rotation_StoredCorrectly()
    {
        var prop = new PropDefinition(
            "sign_001", "meshes/sign.glb",
            0, 0, 8, 2, 90f, 0.5f, false, null);

        Assert.Equal(90f, prop.RotationDegrees);
        Assert.Equal(0.5f, prop.Scale);
    }
}
