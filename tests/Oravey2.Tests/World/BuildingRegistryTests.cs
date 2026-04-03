using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class BuildingRegistryTests
{
    private static BuildingDefinition MakeBuilding(string id, int chunkX = 0, int chunkY = 0)
        => new(id, $"Building {id}", $"meshes/{id}.glb",
            BuildingSize.Small, new[] { (0, 0) }, 1, 1f, null);

    [Fact]
    public void Register_GetById_RoundTrip()
    {
        var registry = new BuildingRegistry();
        var building = MakeBuilding("shop_001");
        registry.Register(building);

        Assert.Equal(building, registry.GetById("shop_001"));
    }

    [Fact]
    public void GetById_NonExistent_ReturnsNull()
    {
        var registry = new BuildingRegistry();
        Assert.Null(registry.GetById("nonexistent"));
    }

    [Fact]
    public void DuplicateId_Throws()
    {
        var registry = new BuildingRegistry();
        var b1 = MakeBuilding("dup_001");
        var b2 = MakeBuilding("dup_001");

        registry.Register(b1);
        Assert.Throws<ArgumentException>(() => registry.Register(b2));
    }

    [Fact]
    public void GetByChunk_ReturnsOnlyBuildingsInThatChunk()
    {
        var registry = new BuildingRegistry();
        var b1 = MakeBuilding("in_chunk");
        var b2 = MakeBuilding("other_chunk");

        registry.RegisterForChunk(b1, 0, 0);
        registry.RegisterForChunk(b2, 1, 0);

        var inChunk00 = registry.GetByChunk(0, 0);
        Assert.Single(inChunk00);
        Assert.Equal("in_chunk", inChunk00[0].Id);

        var inChunk10 = registry.GetByChunk(1, 0);
        Assert.Single(inChunk10);
        Assert.Equal("other_chunk", inChunk10[0].Id);
    }

    [Fact]
    public void GetByChunk_EmptyChunk_ReturnsEmpty()
    {
        var registry = new BuildingRegistry();
        Assert.Empty(registry.GetByChunk(5, 5));
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        var registry = new BuildingRegistry();
        registry.Register(MakeBuilding("a"));
        registry.Register(MakeBuilding("b"));
        registry.Register(MakeBuilding("c"));

        Assert.Equal(3, registry.GetAll().Count);
    }
}
