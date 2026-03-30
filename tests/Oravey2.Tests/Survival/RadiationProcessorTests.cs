using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Survival;

namespace Oravey2.Tests.Survival;

public class RadiationProcessorTests
{
    private static (RadiationProcessor proc, RadiationComponent rad,
        StatsComponent stats, HealthComponent health) Setup()
    {
        var stats = new StatsComponent();
        var bus = new EventBus();
        var level = new LevelComponent(stats, bus);
        var health = new HealthComponent(stats, level, bus);
        var rad = new RadiationComponent();
        var proc = new RadiationProcessor();
        return (proc, rad, stats, health);
    }

    [Fact]
    public void Evaluate_BelowMild_NoModifiers()
    {
        var (proc, rad, stats, health) = Setup();
        rad.Level = 100;
        proc.Evaluate(rad, stats, health, 1f, false);
        Assert.DoesNotContain(stats.Modifiers, m => m.Source == RadiationProcessor.RadMildSource);
    }

    [Fact]
    public void Evaluate_MildThreshold_MinusOneEnd()
    {
        var (proc, rad, stats, health) = Setup();
        rad.Level = 200;
        proc.Evaluate(rad, stats, health, 0f, true); // no delta to avoid decay
        Assert.Contains(stats.Modifiers, m =>
            m.Source == RadiationProcessor.RadMildSource && m.Stat == Stat.Endurance && m.Amount == -1);
    }

    [Fact]
    public void Evaluate_SevereThreshold_MinusTwoEnd_MinusOneStr()
    {
        var (proc, rad, stats, health) = Setup();
        rad.Level = 500;
        proc.Evaluate(rad, stats, health, 0f, true);
        Assert.Contains(stats.Modifiers, m =>
            m.Source == RadiationProcessor.RadSevereEndSource && m.Amount == -2);
        Assert.Contains(stats.Modifiers, m =>
            m.Source == RadiationProcessor.RadSevereStrSource && m.Amount == -1);
    }

    [Fact]
    public void Evaluate_CriticalThreshold_MinusThreeEnd_MinusTwoStr_HPDrain()
    {
        var (proc, rad, stats, health) = Setup();
        rad.Level = 800;
        var hpBefore = health.CurrentHP;
        proc.Evaluate(rad, stats, health, 10f, true); // 10 min → drain = 2*10 = 20
        Assert.Contains(stats.Modifiers, m =>
            m.Source == RadiationProcessor.RadCritEndSource && m.Amount == -3);
        Assert.Contains(stats.Modifiers, m =>
            m.Source == RadiationProcessor.RadCritStrSource && m.Amount == -2);
        Assert.True(health.CurrentHP < hpBefore);
    }

    [Fact]
    public void Evaluate_Lethal_Death()
    {
        var (proc, rad, stats, health) = Setup();
        rad.Level = 1000;
        proc.Evaluate(rad, stats, health, 0f, true);
        Assert.Equal(0, health.CurrentHP);
    }

    [Fact]
    public void Evaluate_NaturalDecay_OutsideRadZone()
    {
        var (proc, rad, stats, health) = Setup();
        rad.Level = 100;
        proc.Evaluate(rad, stats, health, 10f, false); // 10 min → decay 10
        Assert.Equal(90, rad.Level);
    }

    [Fact]
    public void Evaluate_NoDecay_InRadZone()
    {
        var (proc, rad, stats, health) = Setup();
        rad.Level = 100;
        proc.Evaluate(rad, stats, health, 10f, true);
        Assert.Equal(100, rad.Level);
    }

    [Fact]
    public void Evaluate_ThresholdDown_RemovesOldModifiers()
    {
        var (proc, rad, stats, health) = Setup();

        // First evaluate at severe level
        rad.Level = 500;
        proc.Evaluate(rad, stats, health, 0f, true);
        Assert.Contains(stats.Modifiers, m => m.Source == RadiationProcessor.RadSevereEndSource);

        // Drop to below mild
        rad.Level = 100;
        proc.Evaluate(rad, stats, health, 0f, true);
        Assert.DoesNotContain(stats.Modifiers, m => m.Source == RadiationProcessor.RadSevereEndSource);
        Assert.DoesNotContain(stats.Modifiers, m => m.Source == RadiationProcessor.RadSevereStrSource);
    }

    [Fact]
    public void ApplyRadAway_Reduces100()
    {
        var rad = new RadiationComponent { Level = 300 };
        RadiationProcessor.ApplyRadAway(rad);
        Assert.Equal(200, rad.Level);
    }

    [Fact]
    public void ApplyRadAway_FloorsAtZero()
    {
        var rad = new RadiationComponent { Level = 50 };
        RadiationProcessor.ApplyRadAway(rad);
        Assert.Equal(0, rad.Level);
    }
}
