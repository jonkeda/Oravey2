using System.Numerics;
using Oravey2.Core.World.Vegetation;

namespace Oravey2.Tests.Vegetation;

public class TreeSpawnTests
{
    [Fact]
    public void TreeSpawn_ConstructsWithAllFields()
    {
        var spawn = new TreeSpawn(
            Position: new Vector2(10f, 20f),
            Species: TreeSpecies.MutantWillow,
            GrowthStage: 200,
            IsDead: false);

        Assert.Equal(new Vector2(10f, 20f), spawn.Position);
        Assert.Equal(TreeSpecies.MutantWillow, spawn.Species);
        Assert.Equal(200, spawn.GrowthStage);
        Assert.False(spawn.IsDead);
    }

    [Fact]
    public void TreeSpawn_DeadTree_IsDead()
    {
        var spawn = new TreeSpawn(
            Position: new Vector2(5f, 5f),
            Species: TreeSpecies.DeadOak,
            GrowthStage: 128,
            IsDead: true);

        Assert.True(spawn.IsDead);
    }
}
