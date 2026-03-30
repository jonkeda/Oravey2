using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.UI.ViewModels;

namespace Oravey2.Tests.UI;

public class CharacterViewModelTests
{
    private static (StatsComponent stats, SkillsComponent skills, LevelComponent level) SetupDefaults()
    {
        var bus = new EventBus();
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);
        var level = new LevelComponent(stats, bus);
        return (stats, skills, level);
    }

    [Fact]
    public void Create_MapsBaseStats()
    {
        var (stats, skills, level) = SetupDefaults();
        var vm = CharacterViewModel.Create(stats, skills, level);
        Assert.Equal(5, vm.BaseStats[Stat.Strength]);
    }

    [Fact]
    public void Create_MapsEffectiveWithModifier()
    {
        var (stats, skills, level) = SetupDefaults();
        stats.AddModifier(new StatModifier(Stat.Strength, 2, "test"));
        var vm = CharacterViewModel.Create(stats, skills, level);
        Assert.Equal(7, vm.EffectiveStats[Stat.Strength]);
    }

    [Fact]
    public void Create_MapsSkills()
    {
        var (stats, skills, level) = SetupDefaults();
        var vm = CharacterViewModel.Create(stats, skills, level);
        // Default: 10 + 5*2 = 20
        Assert.Equal(20, vm.EffectiveSkills[SkillType.Firearms]);
    }

    [Fact]
    public void Create_MapsLevel()
    {
        var (stats, skills, level) = SetupDefaults();
        var vm = CharacterViewModel.Create(stats, skills, level);
        Assert.Equal(1, vm.Level);
        Assert.Equal(0, vm.CurrentXP);
    }

    [Fact]
    public void Create_MapsAvailablePoints()
    {
        var (stats, skills, level) = SetupDefaults();
        var vm = CharacterViewModel.Create(stats, skills, level);
        Assert.Equal(0, vm.StatPointsAvailable);
    }
}
