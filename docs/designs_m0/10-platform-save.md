# Design: Step 10 — Platform & Save

Implements the save/load data model, slot management, auto-save logic, save migration chain, and settings persistence per [docs/steps/10-platform-save.md](../steps/10-platform-save.md). Architecture from [CLASS_ARCHITECTURE.md](../CLASS_ARCHITECTURE.md) §13. Event flows from [EVENT_FLOWS.md](../events/EVENT_FLOWS.md) §8.

**Depends on:** Steps 1-9 (all game systems that contribute save state)

---

## Deferred to Stride Integration

The following require Stride runtime, platform SDKs, or async I/O and are NOT implemented in this step:

- `SaveService` implementation — requires file system I/O, MessagePack/JSON serialization, async disk writes
- `IPlatformServices` implementations — requires platform-specific APIs (iCloud, Google Play Games, haptics)
- `WindowsPlatformServices`, `iOSPlatformServices`, `AndroidPlatformServices` — platform SDK bindings
- Cloud sync (`SyncToCloudAsync`, `SyncFromCloudAsync`) — requires platform cloud APIs
- `Oravey2.iOS` / `Oravey2.Android` projects — require Stride platform templates + store configuration
- CI/CD pipeline config — GitHub Actions / Azure DevOps build scripts
- Store metadata — app icons, screenshots, descriptions, age ratings
- App lifecycle handling (backgrounding, resume, memory warnings) — requires platform SDK hooks
- Quality presets (render resolution, post-processing tiers) — requires Stride graphics pipeline
- Binary serialization with MessagePack — requires runtime serializer; JSON debug path deferred too

**What IS implemented:** Pure C# logic that the platform layer will consume:

1. **SaveSlot enum** — slot identifiers (AutoSave, QuickSave, Manual1–3)
2. **SaveHeader record** — save metadata (version, timestamp, player name, level, play time)
3. **SaveData class** — complete game state snapshot (player, world, quests, survival, audio/weather)
4. **SerializedItem record** — inventory item representation for save files
5. **SaveSlotInfo record** — slot metadata for the load screen UI
6. **SaveDataBuilder** — collects state from game components into a SaveData instance
7. **SaveDataRestorer** — applies a SaveData instance back to game components
8. **SaveMigration system** — ISaveMigration interface + SaveMigrationChain for version upgrades
9. **AutoSaveTracker** — timer-based and event-driven auto-save trigger logic
10. **GameSettings** — user-configurable settings with defaults and change events

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── Save/
│   ├── SaveSlot.cs                    # enum — slot identifiers
│   ├── SaveHeader.cs                  # record — save metadata
│   ├── SaveData.cs                    # class — full game state snapshot
│   ├── SerializedItem.cs              # record — item save representation
│   ├── SaveSlotInfo.cs                # record — slot UI metadata
│   ├── SaveDataBuilder.cs             # builds SaveData from live components
│   ├── SaveDataRestorer.cs            # restores live components from SaveData
│   ├── ISaveMigration.cs              # interface + SaveMigrationChain
│   └── AutoSaveTracker.cs             # timer + event-driven auto-save triggers
├── Settings/
│   └── GameSettings.cs                # user settings with defaults + events
├── Framework/
│   └── Events/
│       └── GameEvents.cs              # add new events (existing file)
tests/Oravey2.Tests/
├── Save/
│   ├── SaveDataBuilderTests.cs
│   ├── SaveDataRestorerTests.cs
│   ├── SaveMigrationChainTests.cs
│   └── AutoSaveTrackerTests.cs
├── Settings/
│   └── GameSettingsTests.cs
```

**Source files:** 10 new + 1 modified (GameEvents.cs)
**Test files:** 5 new
**Estimated tests:** ~55

---

## Events to Add to GameEvents.cs

```csharp
// Save events:
public readonly record struct SaveCompletedEvent(string SlotName) : IGameEvent;
public readonly record struct LoadCompletedEvent(string SlotName) : IGameEvent;
public readonly record struct AutoSaveTriggeredEvent() : IGameEvent;

// Settings events:
public readonly record struct SettingChangedEvent(string Key, string OldValue, string NewValue) : IGameEvent;
```

---

## Source Code

### 1. SaveSlot.cs

```csharp
namespace Oravey2.Core.Save;

public enum SaveSlot
{
    AutoSave,
    QuickSave,
    Manual1,
    Manual2,
    Manual3
}
```

### 2. SaveHeader.cs

```csharp
namespace Oravey2.Core.Save;

/// <summary>
/// Metadata for a save file. Displayed on the load screen without deserializing the full SaveData.
/// </summary>
public sealed record SaveHeader(
    int FormatVersion,
    string GameVersion,
    DateTime Timestamp,
    string PlayerName,
    int PlayerLevel,
    TimeSpan PlayTime
)
{
    public const int CurrentFormatVersion = 1;
}
```

### 3. SerializedItem.cs

```csharp
namespace Oravey2.Core.Save;

/// <summary>
/// Lightweight representation of an inventory item for serialized save data.
/// </summary>
public sealed record SerializedItem(
    string ItemId,
    int StackCount,
    int? CurrentDurability
);
```

### 4. SaveData.cs

```csharp
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

    // Survival
    public float Hunger { get; set; }
    public float Thirst { get; set; }
    public float Fatigue { get; set; }
    public int Radiation { get; set; }

    // Audio / Weather
    public WeatherState CurrentWeather { get; set; }
    public Dictionary<AudioCategory, float> VolumeSettings { get; set; } = new();
}
```

### 5. SaveSlotInfo.cs

```csharp
namespace Oravey2.Core.Save;

/// <summary>
/// Summary for the load screen UI. One per save slot.
/// </summary>
public sealed record SaveSlotInfo(
    SaveSlot Slot,
    bool IsEmpty,
    SaveHeader? Header
);
```

### 6. SaveDataBuilder.cs

```csharp
using Oravey2.Core.Audio;
using Oravey2.Core.Character.Health;
using Oravey2.Core.Character.Level;
using Oravey2.Core.Character.Perks;
using Oravey2.Core.Character.Skills;
using Oravey2.Core.Character.Stats;
using Oravey2.Core.Inventory;
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
            _data.Skills[s] = skills.GetRank(s);
        return this;
    }

    public SaveDataBuilder WithHealth(HealthComponent health)
    {
        _data.HP = health.Current;
        return this;
    }

    public SaveDataBuilder WithLevel(LevelComponent level)
    {
        _data.Level = level.Level;
        _data.XP = level.CurrentXP;
        return this;
    }

    public SaveDataBuilder WithPerks(PerkManager perks)
    {
        _data.UnlockedPerks = new List<string>(perks.UnlockedPerkIds);
        return this;
    }

    public SaveDataBuilder WithInventory(InventoryComponent inventory)
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
            var equipped = inventory.GetEquipped(slot);
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

    public SaveDataBuilder WithSurvival(SurvivalProcessor survival, RadiationProcessor radiation)
    {
        _data.Hunger = survival.GetNeed("hunger");
        _data.Thirst = survival.GetNeed("thirst");
        _data.Fatigue = survival.GetNeed("fatigue");
        _data.Radiation = radiation.CurrentLevel;
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
```

### 7. SaveDataRestorer.cs

```csharp
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
            skills.SetRank(skill, rank);
    }

    public void RestoreHealth(HealthComponent health)
    {
        health.SetCurrent(_data.HP);
    }

    public void RestoreLevel(LevelComponent level)
    {
        level.SetFromSave(_data.Level, _data.XP);
    }

    public void RestorePerks(PerkManager perks)
    {
        perks.RestoreFromSave(_data.UnlockedPerks);
    }

    public void RestoreSurvival(SurvivalProcessor survival, RadiationProcessor radiation)
    {
        survival.SetNeed("hunger", _data.Hunger);
        survival.SetNeed("thirst", _data.Thirst);
        survival.SetNeed("fatigue", _data.Fatigue);
        radiation.SetLevel(_data.Radiation);
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
}
```

### 8. ISaveMigration.cs

```csharp
namespace Oravey2.Core.Save;

/// <summary>
/// Migrates save data from one format version to the next.
/// </summary>
public interface ISaveMigration
{
    int FromVersion { get; }
    int ToVersion { get; }
    SaveData Migrate(SaveData data);
}

/// <summary>
/// Runs a chain of migrations to bring save data up to the latest format version.
/// </summary>
public sealed class SaveMigrationChain
{
    private readonly SortedList<int, ISaveMigration> _migrations = new();

    public void Register(ISaveMigration migration)
    {
        if (_migrations.ContainsKey(migration.FromVersion))
            throw new InvalidOperationException(
                $"Migration from version {migration.FromVersion} already registered.");
        _migrations[migration.FromVersion] = migration;
    }

    /// <summary>
    /// Migrates save data from its current FormatVersion to the target version.
    /// Throws if a required migration is missing.
    /// </summary>
    public SaveData MigrateToLatest(SaveData data, int targetVersion = -1)
    {
        if (targetVersion < 0)
            targetVersion = SaveHeader.CurrentFormatVersion;

        int current = data.Header.FormatVersion;
        while (current < targetVersion)
        {
            if (!_migrations.TryGetValue(current, out var migration))
                throw new InvalidOperationException(
                    $"No migration registered from version {current}.");

            data = migration.Migrate(data);
            current = data.Header.FormatVersion;
        }
        return data;
    }

    /// <summary>
    /// Returns true if the save data needs migration.
    /// </summary>
    public bool NeedsMigration(SaveData data, int targetVersion = -1)
    {
        if (targetVersion < 0) targetVersion = SaveHeader.CurrentFormatVersion;
        return data.Header.FormatVersion < targetVersion;
    }

    /// <summary>Number of registered migrations.</summary>
    public int Count => _migrations.Count;
}
```

### 9. AutoSaveTracker.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Save;

/// <summary>
/// Tracks auto-save timing and event-driven triggers.
/// Pure logic — the actual save is performed by the caller when ShouldSave is true.
/// </summary>
public sealed class AutoSaveTracker
{
    public const float DefaultIntervalSeconds = 300f; // 5 minutes

    private readonly IEventBus _eventBus;
    private readonly float _intervalSeconds;
    private float _elapsed;
    private bool _pendingSave;
    private bool _enabled;

    /// <summary>Whether auto-save is enabled.</summary>
    public bool Enabled
    {
        get => _enabled;
        set
        {
            _enabled = value;
            if (!value) _pendingSave = false;
        }
    }

    /// <summary>Seconds elapsed since last save.</summary>
    public float Elapsed => _elapsed;

    /// <summary>True if an auto-save should be performed.</summary>
    public bool ShouldSave => _pendingSave;

    /// <summary>Configured interval in seconds.</summary>
    public float IntervalSeconds => _intervalSeconds;

    public AutoSaveTracker(IEventBus eventBus, float intervalSeconds = DefaultIntervalSeconds)
    {
        _eventBus = eventBus;
        _intervalSeconds = intervalSeconds > 0 ? intervalSeconds : DefaultIntervalSeconds;
        _enabled = true;
    }

    /// <summary>
    /// Advances the timer. Sets ShouldSave = true when the interval elapses.
    /// </summary>
    public void Tick(float deltaSec)
    {
        if (!_enabled || deltaSec <= 0) return;

        _elapsed += deltaSec;
        if (_elapsed >= _intervalSeconds)
        {
            _pendingSave = true;
            _eventBus.Publish(new AutoSaveTriggeredEvent());
        }
    }

    /// <summary>
    /// Triggers an immediate auto-save request (e.g., on zone transition or before quit).
    /// </summary>
    public void TriggerNow()
    {
        if (!_enabled) return;
        _pendingSave = true;
        _eventBus.Publish(new AutoSaveTriggeredEvent());
    }

    /// <summary>
    /// Acknowledges that the save was performed. Resets the timer and pending flag.
    /// </summary>
    public void Acknowledge()
    {
        _pendingSave = false;
        _elapsed = 0f;
    }
}
```

### 10. GameSettings.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Settings;

/// <summary>
/// User-configurable game settings stored as string key-value pairs.
/// Publishes SettingChangedEvent on changes. Pure logic — persistence handled externally.
/// </summary>
public sealed class GameSettings
{
    // Well-known keys
    public const string KeyMasterVolume = "audio.master_volume";
    public const string KeyMusicVolume = "audio.music_volume";
    public const string KeySfxVolume = "audio.sfx_volume";
    public const string KeyAmbientVolume = "audio.ambient_volume";
    public const string KeyVoiceVolume = "audio.voice_volume";
    public const string KeyAutoSaveInterval = "save.autosave_interval";
    public const string KeyAutoSaveEnabled = "save.autosave_enabled";
    public const string KeyQualityPreset = "graphics.quality_preset";
    public const string KeyLanguage = "general.language";
    public const string KeyShowDamageNumbers = "ui.show_damage_numbers";
    public const string KeyShowMinimap = "ui.show_minimap";
    public const string KeyCameraZoomSensitivity = "camera.zoom_sensitivity";

    private static readonly Dictionary<string, string> Defaults = new()
    {
        [KeyMasterVolume] = "1.0",
        [KeyMusicVolume] = "1.0",
        [KeySfxVolume] = "1.0",
        [KeyAmbientVolume] = "1.0",
        [KeyVoiceVolume] = "1.0",
        [KeyAutoSaveInterval] = "300",
        [KeyAutoSaveEnabled] = "true",
        [KeyQualityPreset] = "High",
        [KeyLanguage] = "en",
        [KeyShowDamageNumbers] = "true",
        [KeyShowMinimap] = "true",
        [KeyCameraZoomSensitivity] = "1.0",
    };

    private readonly IEventBus _eventBus;
    private readonly Dictionary<string, string> _values;

    public GameSettings(IEventBus eventBus)
    {
        _eventBus = eventBus;
        _values = new Dictionary<string, string>(Defaults);
    }

    /// <summary>
    /// Gets a setting value. Returns the default if the key is known, or null if unknown.
    /// </summary>
    public string? Get(string key)
        => _values.TryGetValue(key, out var val) ? val : null;

    /// <summary>
    /// Gets a setting as float. Returns defaultValue if not parseable.
    /// </summary>
    public float GetFloat(string key, float defaultValue = 0f)
        => float.TryParse(Get(key), out var val) ? val : defaultValue;

    /// <summary>
    /// Gets a setting as bool. Returns defaultValue if not parseable.
    /// </summary>
    public bool GetBool(string key, bool defaultValue = false)
        => bool.TryParse(Get(key), out var val) ? val : defaultValue;

    /// <summary>
    /// Gets a setting as int. Returns defaultValue if not parseable.
    /// </summary>
    public int GetInt(string key, int defaultValue = 0)
        => int.TryParse(Get(key), out var val) ? val : defaultValue;

    /// <summary>
    /// Sets a value and publishes SettingChangedEvent if it differs from the current value.
    /// </summary>
    public void Set(string key, string value)
    {
        var old = Get(key);
        if (old == value) return;

        _values[key] = value;
        _eventBus.Publish(new SettingChangedEvent(key, old ?? "", value));
    }

    /// <summary>
    /// Resets a key to its default value. Publishes event if changed.
    /// </summary>
    public void ResetToDefault(string key)
    {
        if (Defaults.TryGetValue(key, out var def))
            Set(key, def);
    }

    /// <summary>
    /// Resets all settings to defaults. Publishes events for each change.
    /// </summary>
    public void ResetAllDefaults()
    {
        foreach (var (key, def) in Defaults)
            Set(key, def);
    }

    /// <summary>
    /// Returns all current settings as a readonly dictionary (for persistence).
    /// </summary>
    public IReadOnlyDictionary<string, string> GetAll()
        => _values;

    /// <summary>
    /// Loads settings from a dictionary (e.g., from a file). Publishes events for changes.
    /// </summary>
    public void LoadFrom(IReadOnlyDictionary<string, string> settings)
    {
        foreach (var (key, value) in settings)
            Set(key, value);
    }

    /// <summary>All well-known default keys and values.</summary>
    public static IReadOnlyDictionary<string, string> DefaultValues => Defaults;
}
```

---

## Test Tables

### SaveDataBuilderTests (~12 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | Build without header throws | InvalidOperationException |
| 2 | WithHeader sets all fields | Header properties match input |
| 3 | WithStats captures all base stats | Stats dictionary has 7 entries |
| 4 | WithSkills captures all ranks | Skills dictionary has 7 entries |
| 5 | WithHealth captures current HP | HP == health.Current |
| 6 | WithLevel captures level and XP | Level + XP match |
| 7 | WithPerks captures unlocked IDs | UnlockedPerks list matches |
| 8 | WithInventory serializes items | SerializedItem count + fields match |
| 9 | WithInventory captures equipment | Equipment dict matches equipped slots |
| 10 | WithWorldState sets position + time | All position/time fields match |
| 11 | WithSurvival captures needs + radiation | Hunger/Thirst/Fatigue/Radiation match |
| 12 | WithAudio captures weather + volumes | CurrentWeather + VolumeSettings match |

### SaveDataRestorerTests (~10 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | RestoreStats sets all base stats | StatsComponent values match save |
| 2 | RestoreSkills sets all ranks | SkillsComponent values match save |
| 3 | RestoreHealth sets current HP | HealthComponent.Current matches |
| 4 | RestoreLevel sets level and XP | LevelComponent matches |
| 5 | RestorePerks restores IDs | PerkManager.UnlockedPerkIds matches |
| 6 | RestoreSurvival sets needs + radiation | SurvivalProcessor + RadiationProcessor match |
| 7 | RestoreDayNight sets time | DayNightCycleProcessor.InGameHour matches |
| 8 | RestoreWeather forces state | WeatherProcessor.Current matches |
| 9 | RestoreVolume sets all categories | VolumeSettings values match |
| 10 | Property accessors return save data | PlayerChunk, QuestStates, etc. match |

### SaveMigrationChainTests (~10 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | No migrations needed for current version | Returns same data |
| 2 | NeedsMigration true for old version | Returns true |
| 3 | NeedsMigration false for current | Returns false |
| 4 | Single migration upgrades version | FormatVersion incremented |
| 5 | Chained migrations run in order | V1→V2→V3 |
| 6 | Missing migration throws | InvalidOperationException |
| 7 | Duplicate registration throws | InvalidOperationException |
| 8 | Migration applies data changes | Custom field modified |
| 9 | Count returns registered count | Count == N |
| 10 | MigrateToLatest with custom target | Stops at target version |

### AutoSaveTrackerTests (~12 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | Default enabled | Enabled == true |
| 2 | ShouldSave false initially | ShouldSave == false |
| 3 | Tick below interval no trigger | ShouldSave == false |
| 4 | Tick past interval triggers | ShouldSave == true |
| 5 | AutoSaveTriggeredEvent published | Event received |
| 6 | Acknowledge resets timer and flag | ShouldSave false, Elapsed 0 |
| 7 | TriggerNow sets pending | ShouldSave == true |
| 8 | TriggerNow publishes event | Event received |
| 9 | Disabled prevents tick trigger | ShouldSave stays false |
| 10 | Disabled prevents TriggerNow | ShouldSave stays false |
| 11 | Negative delta ignored | Elapsed unchanged |
| 12 | Multiple ticks accumulate | Elapsed grows across calls |

### GameSettingsTests (~11 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | Defaults loaded on construction | All known keys return default values |
| 2 | Get returns value | Correct string returned |
| 3 | Get unknown key returns null | Returns null |
| 4 | GetFloat parses correctly | Returns float value |
| 5 | GetBool parses correctly | Returns bool value |
| 6 | GetInt parses correctly | Returns int value |
| 7 | Set publishes SettingChangedEvent | Event with correct key, old, new |
| 8 | Set same value no event | No event published |
| 9 | ResetToDefault restores value | Value back to default |
| 10 | ResetAllDefaults restores all | All keys back to defaults |
| 11 | LoadFrom applies bulk settings | Multiple values updated |

---

## Execution Order

1. Create `Save/SaveSlot.cs` — no dependencies
2. Create `Save/SaveHeader.cs` — no dependencies
3. Create `Save/SerializedItem.cs` — no dependencies
4. Create `Save/SaveSlotInfo.cs` — depends on SaveSlot, SaveHeader
5. Create `Save/SaveData.cs` — depends on SaveHeader, SerializedItem, enums from Steps 2-9
6. Create `Save/ISaveMigration.cs` — depends on SaveData, SaveHeader
7. Add events to `GameEvents.cs` — no new type dependencies
8. Create `Save/AutoSaveTracker.cs` — depends on IEventBus, events
9. Create `Save/SaveDataBuilder.cs` — depends on SaveData + all game components
10. Create `Save/SaveDataRestorer.cs` — depends on SaveData + all game components
11. Create `Settings/GameSettings.cs` — depends on IEventBus, events
12. Create all test files
13. Run tests — expect ~55 passing
