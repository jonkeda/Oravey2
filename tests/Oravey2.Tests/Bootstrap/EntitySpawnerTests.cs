using Oravey2.Core.Bootstrap.Spawners;

namespace Oravey2.Tests.Bootstrap;

public class EntitySpawnerTests
{
    // ---- NpcSpawnerFactory ----

    [Fact]
    public void NpcSpawnerFactory_CanHandle_NpcPrefix()
    {
        var factory = new NpcSpawnerFactory(null!);
        Assert.True(factory.CanHandle("npc:elder"));
    }

    [Fact]
    public void NpcSpawnerFactory_CanHandle_RejectsEnemy()
    {
        var factory = new NpcSpawnerFactory(null!);
        Assert.False(factory.CanHandle("enemy:radrat"));
    }

    // ---- EnemySpawnerFactory ----

    [Fact]
    public void EnemySpawnerFactory_CanHandle_EnemyPrefix()
    {
        var factory = new EnemySpawnerFactory(null!);
        Assert.True(factory.CanHandle("enemy:radrat"));
    }

    // ---- ZoneExitSpawnerFactory ----

    [Fact]
    public void ZoneExitSpawnerFactory_CanHandle_ZoneExitPrefix()
    {
        var factory = new ZoneExitSpawnerFactory(null!);
        Assert.True(factory.CanHandle("zone_exit:wasteland"));
    }

    // ---- BuildingSpawnerFactory ----

    [Fact]
    public void BuildingSpawnerFactory_CanHandle_BuildingPrefix()
    {
        var factory = new BuildingSpawnerFactory(null!);
        Assert.True(factory.CanHandle("building:ruin_01"));
    }

    [Fact]
    public void BuildingSpawnerFactory_CanHandle_BuildingRuin()
    {
        var factory = new BuildingSpawnerFactory(null!);
        Assert.True(factory.CanHandle("building_ruin"));
    }

    // ---- PropSpawnerFactory ----

    [Fact]
    public void PropSpawnerFactory_CanHandle_PropPrefix()
    {
        var factory = new PropSpawnerFactory(null!);
        Assert.True(factory.CanHandle("prop:barrel"));
    }
}
