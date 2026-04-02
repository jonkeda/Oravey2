using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Combat;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Survival;
using Oravey2.Core.UI;
using Oravey2.Core.UI.ViewModels;
using Oravey2.Core.World;

namespace Oravey2.Tests.UI;

public class HudViewModelTests
{
    private static (HealthComponent health, CombatComponent combat, LevelComponent level,
        DayNightCycleProcessor dayNight, QuickSlotBar quickSlots) SetupDefaults()
    {
        var bus = new EventBus();
        var stats = new StatsComponent();
        var level = new LevelComponent(stats, bus);
        var health = new HealthComponent(stats, level, bus);
        var combat = new CombatComponent();
        var dayNight = new DayNightCycleProcessor(bus, startHour: 8f);
        var quickSlots = new QuickSlotBar();
        return (health, combat, level, dayNight, quickSlots);
    }

    [Fact]
    public void Create_MapsHP()
    {
        var (health, combat, level, dayNight, quickSlots) = SetupDefaults();
        health.TakeDamage(25);
        var vm = HudViewModel.Create(health, combat, level, dayNight, null, null, null, quickSlots);
        Assert.Equal(health.CurrentHP, vm.CurrentHP);
        Assert.Equal(health.MaxHP, vm.MaxHP);
    }

    [Fact]
    public void Create_MapsAP()
    {
        var (health, combat, level, dayNight, quickSlots) = SetupDefaults();
        combat.Spend(3);
        var vm = HudViewModel.Create(health, combat, level, dayNight, null, null, null, quickSlots);
        Assert.Equal(combat.MaxAP, vm.MaxAP);
        Assert.Equal(combat.CurrentAP, vm.CurrentAP);
    }

    [Fact]
    public void Create_MapsLevel()
    {
        var (health, combat, level, dayNight, quickSlots) = SetupDefaults();
        var vm = HudViewModel.Create(health, combat, level, dayNight, null, null, null, quickSlots);
        Assert.Equal(1, vm.Level);
    }

    [Fact]
    public void Create_MapsDayNight()
    {
        var (health, combat, level, dayNight, quickSlots) = SetupDefaults();
        var vm = HudViewModel.Create(health, combat, level, dayNight, null, null, null, quickSlots);
        Assert.Equal(8f, vm.InGameHour);
        Assert.Equal(DayPhase.Day, vm.Phase);
    }

    [Fact]
    public void Create_MapsZoneName()
    {
        var (health, combat, level, dayNight, quickSlots) = SetupDefaults();
        var vm = HudViewModel.Create(health, combat, level, dayNight, "Haven", null, null, quickSlots);
        Assert.Equal("Haven", vm.CurrentZoneName);
    }

    [Fact]
    public void Create_NullSurvival_Defaults()
    {
        var (health, combat, level, dayNight, quickSlots) = SetupDefaults();
        var vm = HudViewModel.Create(health, combat, level, dayNight, null, null, null, quickSlots);
        Assert.Equal(0f, vm.Hunger);
        Assert.Equal(0f, vm.Thirst);
        Assert.Equal(0f, vm.Fatigue);
        Assert.Equal(0, vm.RadiationLevel);
        Assert.False(vm.SurvivalEnabled);
    }

    [Fact]
    public void DefaultPlayer_StartsAtLevel1()
    {
        var (health, combat, level, dayNight, quickSlots) = SetupDefaults();
        var vm = HudViewModel.Create(health, combat, level, dayNight, null, null, null, quickSlots);
        Assert.Equal(1, vm.Level);
    }
}
