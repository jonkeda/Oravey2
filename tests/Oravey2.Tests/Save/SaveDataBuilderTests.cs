using Oravey2.Core.Audio;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Perks;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Save;
using Oravey2.Core.Survival;
using Oravey2.Core.World;

namespace Oravey2.Tests.Save;

public class SaveDataBuilderTests
{
    private readonly EventBus _bus = new();

    [Fact]
    public void BuildWithoutHeaderThrows()
    {
        var builder = new SaveDataBuilder();
        Assert.Throws<InvalidOperationException>(() => builder.Build());
    }

    [Fact]
    public void WithHeaderSetsAllFields()
    {
        var data = new SaveDataBuilder()
            .WithHeader("TestPlayer", 5, TimeSpan.FromHours(2), "1.0.0")
            .Build();

        Assert.Equal("TestPlayer", data.Header.PlayerName);
        Assert.Equal(5, data.Header.PlayerLevel);
        Assert.Equal(TimeSpan.FromHours(2), data.Header.PlayTime);
        Assert.Equal("1.0.0", data.Header.GameVersion);
        Assert.Equal(SaveHeader.CurrentFormatVersion, data.Header.FormatVersion);
    }

    [Fact]
    public void WithStatsCapturesAllBaseStats()
    {
        var stats = new StatsComponent(new Dictionary<Stat, int>
        {
            { Stat.Strength, 8 }, { Stat.Perception, 6 },
            { Stat.Endurance, 7 }, { Stat.Charisma, 4 },
            { Stat.Intelligence, 9 }, { Stat.Agility, 5 },
            { Stat.Luck, 3 }
        });

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithStats(stats)
            .Build();

        Assert.Equal(7, data.Stats.Count);
        Assert.Equal(8, data.Stats[Stat.Strength]);
        Assert.Equal(9, data.Stats[Stat.Intelligence]);
    }

    [Fact]
    public void WithSkillsCapturesAllRanks()
    {
        var stats = new StatsComponent();
        var skills = new SkillsComponent(stats);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithSkills(skills)
            .Build();

        Assert.Equal(7, data.Skills.Count);
        foreach (SkillType s in Enum.GetValues<SkillType>())
            Assert.Equal(skills.GetBase(s), data.Skills[s]);
    }

    [Fact]
    public void WithHealthCapturesCurrentHP()
    {
        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        var health = new HealthComponent(stats, level);
        health.TakeDamage(10);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithHealth(health)
            .Build();

        Assert.Equal(health.CurrentHP, data.HP);
    }

    [Fact]
    public void WithLevelCapturesLevelAndXP()
    {
        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        level.GainXP(50);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithLevel(level)
            .Build();

        Assert.Equal(level.Level, data.Level);
        Assert.Equal(level.CurrentXP, data.XP);
    }

    [Fact]
    public void WithPerksCapturesUnlockedIds()
    {
        var stats = new StatsComponent();
        var level = new LevelComponent(stats);
        var perkDef = new PerkDefinition("perk_test", "Test", "Desc",
            new PerkCondition(1, null, null, null), null, null);
        var perks = new PerkTreeComponent([perkDef], stats, level);
        // Can't unlock through normal flow without perk points, test via restore
        perks.RestoreFromSave(["perk_test"]);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithPerks(perks)
            .Build();

        Assert.Contains("perk_test", data.UnlockedPerks);
    }

    [Fact]
    public void WithInventorySerializesItems()
    {
        var stats = new StatsComponent();
        var itemDef = new ItemDefinition("sword_1", "Sword", "A sword",
            ItemCategory.WeaponMelee, 3.0f, false, 100, Durability: new DurabilityData(50, 1f));
        var item = new ItemInstance(itemDef, 1);
        var inv = new InventoryComponent(stats);
        inv.Add(item);
        var equip = new EquipmentComponent();

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithInventory(inv, equip)
            .Build();

        Assert.Single(data.Inventory);
        Assert.Equal("sword_1", data.Inventory[0].ItemId);
        Assert.Equal(1, data.Inventory[0].StackCount);
        Assert.Equal(50, data.Inventory[0].CurrentDurability);
    }

    [Fact]
    public void WithInventoryCapturesEquipment()
    {
        var stats = new StatsComponent();
        var itemDef = new ItemDefinition("helm_1", "Helmet", "A helmet",
            ItemCategory.Armor, 2.0f, false, 50, Slot: EquipmentSlot.Head);
        var item = new ItemInstance(itemDef);
        var inv = new InventoryComponent(stats);
        var equip = new EquipmentComponent();
        equip.Equip(item, EquipmentSlot.Head);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithInventory(inv, equip)
            .Build();

        Assert.Equal("helm_1", data.Equipment[EquipmentSlot.Head]);
    }

    [Fact]
    public void WithWorldStateSetsPositionAndTime()
    {
        var bus = new EventBus();
        var dayNight = new DayNightCycleProcessor(bus, startHour: 14.5f);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithWorldState(dayNight, 3, 7, 10.5f, 0f, 20.3f)
            .Build();

        Assert.Equal(14.5f, data.InGameHour, 1);
        Assert.Equal(3, data.PlayerChunkX);
        Assert.Equal(7, data.PlayerChunkY);
        Assert.Equal(10.5f, data.PlayerPositionX, 1);
        Assert.Equal(20.3f, data.PlayerPositionZ, 1);
    }

    [Fact]
    public void WithSurvivalCapturesNeedsAndRadiation()
    {
        var survival = new SurvivalComponent { Hunger = 30f, Thirst = 45f, Fatigue = 60f };
        var radiation = new RadiationComponent();
        radiation.Expose(250);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithSurvival(survival, radiation)
            .Build();

        Assert.Equal(30f, data.Hunger, 1);
        Assert.Equal(45f, data.Thirst, 1);
        Assert.Equal(60f, data.Fatigue, 1);
        Assert.Equal(250, data.Radiation);
    }

    [Fact]
    public void WithAudioCapturesWeatherAndVolumes()
    {
        var bus = new EventBus();
        var weather = new WeatherProcessor(bus, random: new Random(42));
        weather.ForceWeather(WeatherState.Foggy);
        var volume = new VolumeSettings(bus);
        volume.SetVolume(AudioCategory.Music, 0.7f);

        var data = new SaveDataBuilder()
            .WithHeader("P", 1, TimeSpan.Zero, "1.0")
            .WithAudio(weather, volume)
            .Build();

        Assert.Equal(WeatherState.Foggy, data.CurrentWeather);
        Assert.Equal(0.7f, data.VolumeSettings[AudioCategory.Music], 2);
    }
}
