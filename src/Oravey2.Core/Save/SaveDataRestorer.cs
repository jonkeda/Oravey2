using Oravey2.Core.Audio;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Perks;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Survival;
using Oravey2.Core.World;

namespace Oravey2.Core.Save;

/// <summary>
/// Restores live game component state from a SaveData snapshot.
/// Pure logic — no I/O. Does not handle chunk/entity loading (that's engine-side).
/// </summary>
public sealed class SaveDataRestorer
{
    private readonly SaveData _data;

    public SaveDataRestorer(SaveData data)
    {
        _data = data;
    }

    public void RestoreStats(StatsComponent stats)
    {
        foreach (var (stat, value) in _data.Stats)
            stats.SetBase(stat, value);
    }

    public void RestoreSkills(SkillsComponent skills)
    {
        foreach (var (skill, rank) in _data.Skills)
            skills.SetBase(skill, rank);
    }

    public void RestoreHealth(HealthComponent health)
    {
        health.SetCurrent(_data.HP);
    }

    public void RestoreLevel(LevelComponent level)
    {
        level.SetFromSave(_data.Level, _data.XP);
    }

    public void RestorePerks(PerkTreeComponent perks)
    {
        perks.RestoreFromSave(_data.UnlockedPerks);
    }

    public void RestoreSurvival(SurvivalComponent survival, RadiationComponent radiation)
    {
        survival.Hunger = _data.Hunger;
        survival.Thirst = _data.Thirst;
        survival.Fatigue = _data.Fatigue;
        survival.Clamp();
        radiation.Level = _data.Radiation;
    }

    public void RestoreDayNight(DayNightCycleProcessor dayNight)
    {
        dayNight.SetTime(_data.InGameHour);
    }

    public void RestoreWeather(WeatherProcessor weather)
    {
        if (_data.CurrentWeather != weather.Current)
            weather.ForceWeather(_data.CurrentWeather);
    }

    public void RestoreVolume(VolumeSettings volume)
    {
        foreach (var (cat, val) in _data.VolumeSettings)
            volume.SetVolume(cat, val);
    }

    /// <summary>Player chunk coordinates from save.</summary>
    public (int ChunkX, int ChunkY) PlayerChunk => (_data.PlayerChunkX, _data.PlayerChunkY);

    /// <summary>Player world position from save.</summary>
    public (float X, float Y, float Z) PlayerPosition =>
        (_data.PlayerPositionX, _data.PlayerPositionY, _data.PlayerPositionZ);

    /// <summary>Quest states to feed into a QuestLogComponent or processor.</summary>
    public Dictionary<string, Quests.QuestStatus> QuestStates => _data.QuestStates;

    /// <summary>Current quest stage per quest.</summary>
    public Dictionary<string, string> QuestStages => _data.QuestStages;

    /// <summary>World flags.</summary>
    public Dictionary<string, bool> WorldFlags => _data.WorldFlags;

    /// <summary>Inventory items to reconstruct.</summary>
    public IReadOnlyList<SerializedItem> InventoryItems => _data.Inventory;

    /// <summary>Equipment mapping.</summary>
    public IReadOnlyDictionary<Inventory.Items.EquipmentSlot, string?> Equipment => _data.Equipment;

    /// <summary>Discovered fast-travel locations.</summary>
    public IReadOnlyList<string> DiscoveredLocationIds => _data.DiscoveredLocationIds;

    /// <summary>Faction reputation scores.</summary>
    public IReadOnlyDictionary<string, int> FactionRep => _data.FactionRep;

    /// <summary>Unlocked perk IDs.</summary>
    public IReadOnlyList<string> UnlockedPerks => _data.UnlockedPerks;

    /// <summary>Player currency (caps).</summary>
    public int Caps => _data.Caps;
}
