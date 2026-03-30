using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Survival;

namespace Oravey2.Tests.Survival;

public class SurvivalProcessorTests
{
    private static (SurvivalProcessor proc, SurvivalComponent survival,
        StatsComponent stats, HealthComponent health) Setup()
    {
        var stats = new StatsComponent();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var health = new HealthComponent(stats, level, bus);
        var survival = new SurvivalComponent();
        var proc = new SurvivalProcessor(bus);
        return (proc, survival, stats, health);
    }

    [Fact]
    public void Tick_IncrementsHunger()
    {
        var (proc, survival, stats, health) = Setup();
        proc.Tick(survival, stats, health, 1f); // 1 hour
        Assert.Equal(2.0f, survival.Hunger, 0.01f);
    }

    [Fact]
    public void Tick_IncrementsThirst()
    {
        var (proc, survival, stats, health) = Setup();
        proc.Tick(survival, stats, health, 1f);
        Assert.Equal(3.0f, survival.Thirst, 0.01f);
    }

    [Fact]
    public void Tick_IncrementsFatigue()
    {
        var (proc, survival, stats, health) = Setup();
        proc.Tick(survival, stats, health, 1f);
        Assert.Equal(1.5f, survival.Fatigue, 0.01f);
    }

    [Fact]
    public void Tick_Disabled_NoChange()
    {
        var (proc, survival, stats, health) = Setup();
        survival.Enabled = false;
        proc.Tick(survival, stats, health, 10f);
        Assert.Equal(0f, survival.Hunger);
        Assert.Equal(0f, survival.Thirst);
        Assert.Equal(0f, survival.Fatigue);
    }

    [Fact]
    public void Tick_ClampsAt100()
    {
        var (proc, survival, stats, health) = Setup();
        proc.Tick(survival, stats, health, 100f); // huge delta
        Assert.Equal(100f, survival.Hunger);
        Assert.Equal(100f, survival.Thirst);
        Assert.Equal(100f, survival.Fatigue);
    }

    [Fact]
    public void Tick_SatisfiedHunger_StrBuff()
    {
        var (proc, survival, stats, health) = Setup();
        survival.Hunger = 0; // Satisfied
        proc.Tick(survival, stats, health, 0.001f); // tiny tick, stays Satisfied
        Assert.Contains(stats.Modifiers, m =>
            m.Source == SurvivalProcessor.HungerBuffSource && m.Stat == Stat.Strength && m.Amount == 1);
    }

    [Fact]
    public void Tick_DeprivedHunger_StrDebuff()
    {
        var (proc, survival, stats, health) = Setup();
        survival.Hunger = 60; // Deprived
        proc.Tick(survival, stats, health, 0.001f);
        Assert.Contains(stats.Modifiers, m =>
            m.Source == SurvivalProcessor.HungerDebuffSource && m.Stat == Stat.Strength && m.Amount == -1);
    }

    [Fact]
    public void Tick_CriticalHunger_DrainsHP()
    {
        var (proc, survival, stats, health) = Setup();
        survival.Hunger = 80; // Critical
        var hpBefore = health.CurrentHP;
        proc.Tick(survival, stats, health, 1f); // 1 hour = 60 min, drain = 2*60 = 120
        Assert.True(health.CurrentHP < hpBefore);
    }

    [Fact]
    public void Tick_CriticalThirst_DrainsHP()
    {
        var (proc, survival, stats, health) = Setup();
        survival.Thirst = 80; // Critical
        var hpBefore = health.CurrentHP;
        proc.Tick(survival, stats, health, 1f); // 1 hour = 60 min, drain = 3*60 = 180
        Assert.True(health.CurrentHP < hpBefore);
    }

    [Fact]
    public void Tick_ThresholdChange_RemovesOldModifier()
    {
        var (proc, survival, stats, health) = Setup();
        // Start Satisfied
        survival.Hunger = 0;
        proc.Tick(survival, stats, health, 0.001f);
        Assert.Contains(stats.Modifiers, m => m.Source == SurvivalProcessor.HungerBuffSource);

        // Move to Normal (26-50)
        survival.Hunger = 30;
        proc.Tick(survival, stats, health, 0.001f);
        Assert.DoesNotContain(stats.Modifiers, m => m.Source == SurvivalProcessor.HungerBuffSource);
    }

    [Fact]
    public void Tick_DeprivedFatigue_AgiDebuff()
    {
        var (proc, survival, stats, health) = Setup();
        survival.Fatigue = 60; // Deprived
        proc.Tick(survival, stats, health, 0.001f);
        Assert.Contains(stats.Modifiers, m =>
            m.Source == SurvivalProcessor.FatigueDebuffSource && m.Stat == Stat.Agility && m.Amount == -1);
    }

    [Fact]
    public void RestoreNeed_Hunger_ReducesValue()
    {
        var survival = new SurvivalComponent { Hunger = 50 };
        SurvivalProcessor.RestoreNeed(survival, "hunger", 25f);
        Assert.Equal(25f, survival.Hunger);
    }

    [Fact]
    public void RestoreNeed_Thirst_ReducesValue()
    {
        var survival = new SurvivalComponent { Thirst = 60 };
        SurvivalProcessor.RestoreNeed(survival, "thirst", 30f);
        Assert.Equal(30f, survival.Thirst);
    }

    [Fact]
    public void RestoreNeed_FloorsAtZero()
    {
        var survival = new SurvivalComponent { Hunger = 10 };
        SurvivalProcessor.RestoreNeed(survival, "hunger", 50f);
        Assert.Equal(0f, survival.Hunger);
    }
}
