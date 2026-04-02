using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.World;

namespace Oravey2.Tests.Combat;

public class KillTrackerTests
{
    [Fact]
    public void RegisterCounter_OnDeath_IncrementsFlag()
    {
        var world = new WorldStateService();
        var bus = new EventBus();
        var tracker = new KillTracker(world, bus);
        tracker.RegisterCounter("radrat", "rats_killed");

        bus.Publish(new EntityDiedEvent("enemy_1", "radrat"));

        Assert.Equal(1, world.GetCounter("rats_killed"));
    }

    [Fact]
    public void RegisterCounter_MultipleDeaths_Accumulates()
    {
        var world = new WorldStateService();
        var bus = new EventBus();
        var tracker = new KillTracker(world, bus);
        tracker.RegisterCounter("radrat", "rats_killed");

        bus.Publish(new EntityDiedEvent("enemy_1", "radrat"));
        bus.Publish(new EntityDiedEvent("enemy_2", "radrat"));
        bus.Publish(new EntityDiedEvent("enemy_3", "radrat"));

        Assert.Equal(3, world.GetCounter("rats_killed"));
    }

    [Fact]
    public void RegisterFlag_OnBossDeath_SetsFlag()
    {
        var world = new WorldStateService();
        var bus = new EventBus();
        var tracker = new KillTracker(world, bus);
        tracker.RegisterFlag("scar", "scar_killed");

        bus.Publish(new EntityDiedEvent("boss_scar", "scar"));

        Assert.True(world.GetFlag("scar_killed"));
    }

    [Fact]
    public void UnregisteredTag_Ignored()
    {
        var world = new WorldStateService();
        var bus = new EventBus();
        var tracker = new KillTracker(world, bus);
        tracker.RegisterCounter("radrat", "rats_killed");

        bus.Publish(new EntityDiedEvent("enemy_1", "raider"));

        Assert.Equal(0, world.GetCounter("rats_killed"));
    }

    [Fact]
    public void NullTag_Ignored()
    {
        var world = new WorldStateService();
        var bus = new EventBus();
        var tracker = new KillTracker(world, bus);
        tracker.RegisterCounter("radrat", "rats_killed");

        bus.Publish(new EntityDiedEvent("enemy_1", null));

        Assert.Equal(0, world.GetCounter("rats_killed"));
    }

    [Fact]
    public void BothCounterAndFlag_SameTag_BothFire()
    {
        var world = new WorldStateService();
        var bus = new EventBus();
        var tracker = new KillTracker(world, bus);
        tracker.RegisterCounter("scar", "scar_kills");
        tracker.RegisterFlag("scar", "scar_killed");

        bus.Publish(new EntityDiedEvent("boss_scar", "scar"));

        Assert.Equal(1, world.GetCounter("scar_kills"));
        Assert.True(world.GetFlag("scar_killed"));
    }
}
