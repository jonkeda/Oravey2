using Oravey2.Core.Audio;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Perks;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Quests;
using Oravey2.Core.Save;
using Oravey2.Core.Survival;
using Oravey2.Core.World;

namespace Oravey2.Tests.Save;

public class SaveDataRestorerTests
{
    private readonly EventBus _bus = new();

    private SaveData CreateTestSaveData()
    {
        return new SaveData
        {
            Header = new SaveHeader(1, "1.0.0", DateTime.UtcNow, "TestPlayer", 5, TimeSpan.FromHours(1)),
            Stats = new Dictionary<Stat, int>
            {
                { Stat.Strength, 8 }, { Stat.Perception, 6 },
                { Stat.Endurance, 7 }, { Stat.Charisma, 4 },
                { Stat.Intelligence, 9 }, { Stat.Agility, 5 },
                { Stat.Luck, 3 }
            },
            Skills = new Dictionary<SkillType, int>
            {
                { SkillType.Firearms, 30 }, { SkillType.Melee, 25 },
                { SkillType.Survival, 40 }, { SkillType.Science, 35 },
                { SkillType.Speech, 20 }, { SkillType.Stealth, 15 },
                { SkillType.Mechanics, 28 }
            },
            HP = 75,
            Level = 5,
            XP = 120,
            UnlockedPerks = ["perk_a", "perk_b"],
            Inventory = [new SerializedItem("item_1", 3, 40)],
            Equipment = new Dictionary<EquipmentSlot, string?>
            {
                { EquipmentSlot.Head, "helm_1" }
            },
            InGameHour = 14.5f,
            PlayerChunkX = 3,
            PlayerChunkY = 7,
            PlayerPositionX = 10f,
            PlayerPositionY = 0f,
            PlayerPositionZ = 20f,
            QuestStates = new Dictionary<string, QuestStatus>
            {
                { "quest_1", QuestStatus.Active }
            },
            QuestStages = new Dictionary<string, string>
            {
                { "quest_1", "stage_2" }
            },
            WorldFlags = new Dictionary<string, bool>
            {
                { "flag_bridge_repaired", true }
            },
            Hunger = 30f,
            Thirst = 45f,
            Fatigue = 60f,
            Radiation = 250,
            CurrentWeather = WeatherState.DustStorm,
            VolumeSettings = new Dictionary<AudioCategory, float>
            {
                { AudioCategory.Master, 0.8f },
                { AudioCategory.Music, 0.6f }
            },
            DiscoveredLocationIds = ["loc_1", "loc_2"],
            FactionRep = new Dictionary<string, int>
            {
                { "faction_a", 50 }
            }
        };
    }

    [Fact]
    public void RestoreStatsSetsAllBaseStats()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var stats = new StatsComponent();

        restorer.RestoreStats(stats);

        Assert.Equal(8, stats.GetBase(Stat.Strength));
        Assert.Equal(9, stats.GetBase(Stat.Intelligence));
        Assert.Equal(3, stats.GetBase(Stat.Luck));
    }

    [Fact]
    public void RestoreSkillsSetsAllRanks()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);

        restorer.RestoreSkills(skills);

        Assert.Equal(30, skills.GetBase(SkillType.Firearms));
        Assert.Equal(40, skills.GetBase(SkillType.Survival));
    }

    [Fact]
    public void RestoreHealthSetsCurrentHP()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        var health = new HealthComponent(stats, level);

        restorer.RestoreHealth(health);

        Assert.Equal(75, health.CurrentHP);
    }

    [Fact]
    public void RestoreLevelSetsLevelAndXP()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var stats = new StatsComponent();
        var level = new LevelComponent(stats);

        restorer.RestoreLevel(level);

        Assert.Equal(5, level.Level);
        Assert.Equal(120, level.CurrentXP);
    }

    [Fact]
    public void RestorePerksRestoresIds()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        var perks = new PerkTreeComponent([], stats, level);

        restorer.RestorePerks(perks);

        Assert.Contains("perk_a", perks.UnlockedPerks);
        Assert.Contains("perk_b", perks.UnlockedPerks);
    }

    [Fact]
    public void RestoreSurvivalSetsNeedsAndRadiation()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var survival = new SurvivalComponent();
        var radiation = new RadiationComponent();

        restorer.RestoreSurvival(survival, radiation);

        Assert.Equal(30f, survival.Hunger, 1);
        Assert.Equal(45f, survival.Thirst, 1);
        Assert.Equal(60f, survival.Fatigue, 1);
        Assert.Equal(250, radiation.Level);
    }

    [Fact]
    public void RestoreDayNightSetsTime()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var dayNight = new DayNightCycleProcessor(_bus);

        restorer.RestoreDayNight(dayNight);

        Assert.Equal(14.5f, dayNight.InGameHour, 1);
    }

    [Fact]
    public void RestoreWeatherForcesState()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var weather = new WeatherProcessor(_bus, random: new Random(42));

        restorer.RestoreWeather(weather);

        Assert.Equal(WeatherState.DustStorm, weather.Current);
    }

    [Fact]
    public void RestoreVolumeSetsAllCategories()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);
        var volume = new VolumeSettings(_bus);

        restorer.RestoreVolume(volume);

        Assert.Equal(0.8f, volume.GetVolume(AudioCategory.Master), 2);
        Assert.Equal(0.6f, volume.GetVolume(AudioCategory.Music), 2);
    }

    [Fact]
    public void PropertyAccessorsReturnSaveData()
    {
        var data = CreateTestSaveData();
        var restorer = new SaveDataRestorer(data);

        Assert.Equal((3, 7), restorer.PlayerChunk);
        Assert.Equal((10f, 0f, 20f), restorer.PlayerPosition);
        Assert.Equal(QuestStatus.Active, restorer.QuestStates["quest_1"]);
        Assert.Equal("stage_2", restorer.QuestStages["quest_1"]);
        Assert.True(restorer.WorldFlags["flag_bridge_repaired"]);
        Assert.Single(restorer.InventoryItems);
        Assert.Equal("helm_1", restorer.Equipment[EquipmentSlot.Head]);
        Assert.Equal(2, restorer.DiscoveredLocationIds.Count);
        Assert.Equal(50, restorer.FactionRep["faction_a"]);
        Assert.Equal(2, restorer.UnlockedPerks.Count);
    }
}
