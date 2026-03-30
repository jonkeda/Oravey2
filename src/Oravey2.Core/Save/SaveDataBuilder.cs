using Oravey2.Core.Audio;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Perks;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Core;
using Oravey2.Core.Inventory.Equipment;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Quests;
using Oravey2.Core.Survival;
using Oravey2.Core.World;

namespace Oravey2.Core.Save;

/// <summary>
/// Builds a SaveData instance by reading live game component states.
/// Pure logic — no I/O.
/// </summary>
public sealed class SaveDataBuilder
{
    private readonly SaveData _data = new();

    public SaveDataBuilder WithHeader(string playerName, int playerLevel, TimeSpan playTime, string gameVersion)
    {
        _data.Header = new SaveHeader(
            SaveHeader.CurrentFormatVersion, gameVersion,
            DateTime.UtcNow, playerName, playerLevel, playTime);
        return this;
    }

    public SaveDataBuilder WithStats(StatsComponent stats)
    {
        foreach (Stat s in Enum.GetValues<Stat>())
            _data.Stats[s] = stats.GetBase(s);
        return this;
    }

    public SaveDataBuilder WithSkills(SkillsComponent skills)
    {
        foreach (SkillType s in Enum.GetValues<SkillType>())
            _data.Skills[s] = skills.GetBase(s);
        return this;
    }

    public SaveDataBuilder WithHealth(HealthComponent health)
    {
        _data.HP = health.CurrentHP;
        return this;
    }

    public SaveDataBuilder WithLevel(LevelComponent level)
    {
        _data.Level = level.Level;
        _data.XP = level.CurrentXP;
        return this;
    }

    public SaveDataBuilder WithPerks(PerkTreeComponent perks)
    {
        _data.UnlockedPerks = new List<string>(perks.UnlockedPerks);
        return this;
    }

    public SaveDataBuilder WithInventory(InventoryComponent inventory, EquipmentComponent equipment)
    {
        _data.Inventory.Clear();
        foreach (var item in inventory.Items)
        {
            _data.Inventory.Add(new SerializedItem(
                item.Definition.Id, item.StackCount, item.CurrentDurability));
        }

        _data.Equipment.Clear();
        foreach (EquipmentSlot slot in Enum.GetValues<EquipmentSlot>())
        {
            var equipped = equipment.GetEquipped(slot);
            _data.Equipment[slot] = equipped?.Definition.Id;
        }
        return this;
    }

    public SaveDataBuilder WithWorldState(DayNightCycleProcessor dayNight, int chunkX, int chunkY,
        float posX, float posY, float posZ)
    {
        _data.InGameHour = dayNight.InGameHour;
        _data.PlayerChunkX = chunkX;
        _data.PlayerChunkY = chunkY;
        _data.PlayerPositionX = posX;
        _data.PlayerPositionY = posY;
        _data.PlayerPositionZ = posZ;
        return this;
    }

    public SaveDataBuilder WithDiscoveredLocations(IEnumerable<string> locationIds)
    {
        _data.DiscoveredLocationIds = new List<string>(locationIds);
        return this;
    }

    public SaveDataBuilder WithQuestStates(Dictionary<string, QuestStatus> states,
        Dictionary<string, string> stages, Dictionary<string, bool> worldFlags)
    {
        _data.QuestStates = new Dictionary<string, QuestStatus>(states);
        _data.QuestStages = new Dictionary<string, string>(stages);
        _data.WorldFlags = new Dictionary<string, bool>(worldFlags);
        return this;
    }

    public SaveDataBuilder WithSurvival(SurvivalComponent survival, RadiationComponent radiation)
    {
        _data.Hunger = survival.Hunger;
        _data.Thirst = survival.Thirst;
        _data.Fatigue = survival.Fatigue;
        _data.Radiation = radiation.Level;
        return this;
    }

    public SaveDataBuilder WithAudio(WeatherProcessor weather, VolumeSettings volume)
    {
        _data.CurrentWeather = weather.Current;
        _data.VolumeSettings.Clear();
        foreach (AudioCategory cat in Enum.GetValues<AudioCategory>())
            _data.VolumeSettings[cat] = volume.GetVolume(cat);
        return this;
    }

    public SaveData Build()
    {
        if (_data.Header == null)
            throw new InvalidOperationException("SaveHeader is required. Call WithHeader() first.");
        return _data;
    }
}
