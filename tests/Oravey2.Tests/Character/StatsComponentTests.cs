using Oravey2.Core.Character.Stats;

namespace Oravey2.Tests.Character;

public class StatsComponentTests
{
    [Fact]
    public void DefaultStats_AllFive()
    {
        var stats = new StatsComponent();
        foreach (var stat in Enum.GetValues<Stat>())
            Assert.Equal(5, stats.GetBase(stat));
    }

    [Fact]
    public void CustomAllocation_Applies()
    {
        var initial = new Dictionary<Stat, int>
        {
            { Stat.Strength, 8 },
            { Stat.Perception, 3 },
            { Stat.Endurance, 6 },
            { Stat.Charisma, 2 },
            { Stat.Intelligence, 7 },
            { Stat.Agility, 1 },
            { Stat.Luck, 1 },
        };
        var stats = new StatsComponent(initial);

        Assert.Equal(8, stats.GetBase(Stat.Strength));
        Assert.Equal(3, stats.GetBase(Stat.Perception));
        Assert.Equal(1, stats.GetBase(Stat.Agility));
    }

    [Fact]
    public void SetBase_ClampsTo1_10()
    {
        var stats = new StatsComponent();
        stats.SetBase(Stat.Strength, 0);
        Assert.Equal(1, stats.GetBase(Stat.Strength));

        stats.SetBase(Stat.Strength, 15);
        Assert.Equal(10, stats.GetBase(Stat.Strength));
    }

    [Fact]
    public void Modifier_IncreasesEffective()
    {
        var stats = new StatsComponent();
        var mod = new StatModifier(Stat.Strength, 2, "test");
        stats.AddModifier(mod);

        Assert.Equal(7, stats.GetEffective(Stat.Strength));
        Assert.Equal(5, stats.GetBase(Stat.Strength));
    }

    [Fact]
    public void RemoveModifier_RestoresEffective()
    {
        var stats = new StatsComponent();
        var mod = new StatModifier(Stat.Strength, 2, "test");
        stats.AddModifier(mod);
        stats.RemoveModifier(mod);

        Assert.Equal(5, stats.GetEffective(Stat.Strength));
    }

    [Fact]
    public void MultipleModifiers_Stack()
    {
        var stats = new StatsComponent();
        stats.AddModifier(new StatModifier(Stat.Strength, 1, "a"));
        stats.AddModifier(new StatModifier(Stat.Strength, 1, "b"));

        Assert.Equal(7, stats.GetEffective(Stat.Strength));
    }

    [Fact]
    public void IsValidAllocation_28Points_ReturnsTrue()
    {
        var alloc = new Dictionary<Stat, int>
        {
            { Stat.Strength, 4 }, { Stat.Perception, 4 }, { Stat.Endurance, 4 },
            { Stat.Charisma, 4 }, { Stat.Intelligence, 4 }, { Stat.Agility, 4 },
            { Stat.Luck, 4 },
        };
        Assert.True(StatsComponent.IsValidAllocation(alloc));
    }

    [Fact]
    public void IsValidAllocation_WrongTotal_ReturnsFalse()
    {
        var alloc = new Dictionary<Stat, int>
        {
            { Stat.Strength, 5 }, { Stat.Perception, 5 }, { Stat.Endurance, 5 },
            { Stat.Charisma, 5 }, { Stat.Intelligence, 5 }, { Stat.Agility, 5 },
            { Stat.Luck, 5 },
        };
        Assert.False(StatsComponent.IsValidAllocation(alloc));
    }

    [Fact]
    public void IsValidAllocation_StatBelowMin_ReturnsFalse()
    {
        var alloc = new Dictionary<Stat, int>
        {
            { Stat.Strength, 0 }, { Stat.Perception, 4 }, { Stat.Endurance, 4 },
            { Stat.Charisma, 4 }, { Stat.Intelligence, 4 }, { Stat.Agility, 4 },
            { Stat.Luck, 8 },
        };
        Assert.False(StatsComponent.IsValidAllocation(alloc));
    }
}
