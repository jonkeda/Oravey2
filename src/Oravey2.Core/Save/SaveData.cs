using Oravey2.Core.Audio;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory.Items;
using Oravey2.Core.Quests;

namespace Oravey2.Core.Save;

/// <summary>
/// Complete game state snapshot for serialization.
/// </summary>
public sealed class SaveData
{
    public SaveHeader Header { get; set; } = null!;

    // Player
    public Dictionary<Stat, int> Stats { get; set; } = new();
    public Dictionary<SkillType, int> Skills { get; set; } = new();
    public int HP { get; set; }
    public int Level { get; set; }
    public int XP { get; set; }
    public List<string> UnlockedPerks { get; set; } = new();
    public List<SerializedItem> Inventory { get; set; } = new();
    public Dictionary<EquipmentSlot, string?> Equipment { get; set; } = new();
    public Dictionary<string, int> FactionRep { get; set; } = new();

    // World
    public float InGameHour { get; set; }
    public int PlayerChunkX { get; set; }
    public int PlayerChunkY { get; set; }
    public float PlayerPositionX { get; set; }
    public float PlayerPositionY { get; set; }
    public float PlayerPositionZ { get; set; }
    public Dictionary<string, Dictionary<string, bool>> ChunkModifications { get; set; } = new();
    public List<string> DiscoveredLocationIds { get; set; } = new();

    // Quests
    public Dictionary<string, QuestStatus> QuestStates { get; set; } = new();
    public Dictionary<string, string> QuestStages { get; set; } = new();
    public Dictionary<string, bool> WorldFlags { get; set; } = new();
    public Dictionary<string, int> WorldCounters { get; set; } = new();

    // Currency
    public int Caps { get; set; }

    // Survival
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Fatigue { get; set; }
    public int Radiation { get; set; }

    // Audio / Weather
    public WeatherState CurrentWeather { get; set; }
    public Dictionary<AudioCategory, float> VolumeSettings { get; set; } = new();
}
