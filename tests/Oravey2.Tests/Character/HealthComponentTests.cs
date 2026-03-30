using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Stats;

namespace Oravey2.Tests.Character;

public class HealthComponentTests
{
    private static (StatsComponent stats, LevelComponent level) CreateDeps()
    {
        var stats = new StatsComponent(); // End=5
        var level = new LevelComponent(stats);
        return (stats, level);
    }

    [Fact]
    public void InitialHP_EqualsMaxHP()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);

        // MaxHP = 50 + 5*10 + 1*5 = 105
        Assert.Equal(105, health.MaxHP);
        Assert.Equal(105, health.CurrentHP);
    }

    [Fact]
    public void TakeDamage_ReducesHP()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.TakeDamage(20);

        Assert.Equal(85, health.CurrentHP);
    }

    [Fact]
    public void TakeDamage_ClampsAtZero()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.TakeDamage(200);

        Assert.Equal(0, health.CurrentHP);
    }

    [Fact]
    public void TakeDamage_NegativeIgnored()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.TakeDamage(-5);

        Assert.Equal(105, health.CurrentHP);
    }

    [Fact]
    public void Heal_IncreasesHP()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.TakeDamage(50);
        health.Heal(10);

        Assert.Equal(65, health.CurrentHP);
    }

    [Fact]
    public void Heal_ClampsAtMax()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.TakeDamage(10);
        health.Heal(999);

        Assert.Equal(105, health.CurrentHP);
    }

    [Fact]
    public void IsAlive_TrueAboveZero()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.TakeDamage(104);

        Assert.True(health.IsAlive);
    }

    [Fact]
    public void IsAlive_FalseAtZero()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.TakeDamage(105);

        Assert.False(health.IsAlive);
    }

    [Fact]
    public void ApplyEffect_AddsToList()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.ApplyEffect(new StatusEffect(StatusEffectType.Poisoned, 10f, 1f));

        Assert.Single(health.ActiveEffects);
        Assert.Equal(StatusEffectType.Poisoned, health.ActiveEffects[0].Type);
    }

    [Fact]
    public void RemoveEffect_RemovesFromList()
    {
        var (stats, level) = CreateDeps();
        var health = new HealthComponent(stats, level);
        health.ApplyEffect(new StatusEffect(StatusEffectType.Poisoned, 10f, 1f));
        health.RemoveEffect(StatusEffectType.Poisoned);

        Assert.Empty(health.ActiveEffects);
    }
}
