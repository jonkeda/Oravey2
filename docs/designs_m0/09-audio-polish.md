# Design: Step 09 — Audio & Polish

Implements adaptive music layer logic, weather system, volume management, and audio-lookup tables per [docs/steps/09-audio-polish.md](../steps/09-audio-polish.md). Architecture from [CLASS_ARCHITECTURE.md](../CLASS_ARCHITECTURE.md) §12. Floating text constants from [GAME_CONSTANTS.md](../constants/GAME_CONSTANTS.md) §10.

**Depends on:** Steps 1-8 (GameState, TileType, BiomeType, ZoneDefinition, IEventBus)

---

## Deferred to Stride Integration

The following require Stride audio/rendering runtime and are NOT implemented in this step:

- `AudioService` implementation — requires Stride `SoundInstance`, `AudioEmitter`, `AudioEngine`
- `MusicStateProcessor` SyncScript — requires Stride audio playback for actual crossfade
- `AmbientAudioProcessor` SyncScript — requires Stride audio looping + crossfade
- `FootstepProcessor` SyncScript — requires Stride entity position + audio playback
- SFX pooling / positional audio — requires `AudioEmitterComponent`, 3D spatialization
- UI audio (click, hover, open/close, error jingle) — requires Stride UI event hooks
- Post-processing pipeline (desaturation, film grain, vignette, bloom) — requires Stride `RenderFeature`
- Weather VFX / particle systems (dust storms, acid rain, fog volumes) — requires Stride `ParticleSystem`
- Screen shake — requires Stride camera transform manipulation
- Damage numbers / floating text rendering — requires Stride UI or `SpriteBatch`
- Mobile optimization pass — requires Stride platform-specific build configuration
- Audio quality scaling — requires Stride audio format selection

**What IS implemented:** Pure C# logic that the Stride audio/VFX scripts will consume:

1. **AudioCategory enum** — volume category identifiers (Master, Music, SFX, Ambient, Voice)
2. **MusicLayer enum + MusicLayerDefinition** — layer identifiers and fade-time metadata
3. **MusicLayerResolver** — determines which music layers should be active given current GameState + context flags
4. **WeatherState enum** — weather type identifiers (Clear, Foggy, DustStorm, AcidRain)
5. **WeatherProcessor** — random weather transitions with configurable timing + weighted probabilities
6. **VolumeSettings** — per-category volume management with master multiplier and change events
7. **FootstepSurfaceMap** — maps TileType to footstep SFX identifiers
8. **AmbientZoneMap** — maps BiomeType to ambient sound identifiers

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── Audio/
│   ├── AudioCategory.cs              # enum — volume categories
│   ├── MusicLayer.cs                 # enum + MusicLayerDefinition record
│   ├── MusicLayerResolver.cs         # GameState + context → active layer set
│   ├── WeatherState.cs               # enum — weather types
│   ├── WeatherProcessor.cs           # random weather transitions + events
│   ├── VolumeSettings.cs             # per-category volume with master multiplier
│   ├── FootstepSurfaceMap.cs         # TileType → SFX id lookup
│   └── AmbientZoneMap.cs             # BiomeType → ambient id lookup
├── Framework/
│   └── Events/
│       └── GameEvents.cs             # add new events (existing file)
tests/Oravey2.Tests/
├── Audio/
│   ├── MusicLayerResolverTests.cs
│   ├── WeatherProcessorTests.cs
│   ├── VolumeSettingsTests.cs
│   ├── FootstepSurfaceMapTests.cs
│   └── AmbientZoneMapTests.cs
```

**Source files:** 8 new + 1 modified (GameEvents.cs)
**Test files:** 5 new
**Estimated tests:** ~50

---

## Events to Add to GameEvents.cs

```csharp
// Audio events:
using Oravey2.Core.Audio;

public readonly record struct WeatherChangedEvent(
    WeatherState OldState, WeatherState NewState) : IGameEvent;
public readonly record struct MusicLayerActivatedEvent(
    MusicLayer Layer, float FadeDuration) : IGameEvent;
public readonly record struct MusicLayerDeactivatedEvent(
    MusicLayer Layer, float FadeDuration) : IGameEvent;
public readonly record struct VolumeChangedEvent(
    AudioCategory Category, float OldVolume, float NewVolume) : IGameEvent;
```

---

## Source Code

### 1. AudioCategory.cs

```csharp
namespace Oravey2.Core.Audio;

public enum AudioCategory
{
    Master,
    Music,
    SFX,
    Ambient,
    Voice
}
```

### 2. MusicLayer.cs

```csharp
namespace Oravey2.Core.Audio;

public enum MusicLayer
{
    Base,
    Exploration,
    Tension,
    Combat,
    Eerie
}

/// <summary>
/// Metadata for a music layer: trigger description and crossfade duration.
/// </summary>
public sealed record MusicLayerDefinition(MusicLayer Layer, float FadeDurationSeconds)
{
    /// <summary>
    /// All layer definitions keyed by MusicLayer.
    /// Fade times from Step 09 spec: Base=0, Exploration=2, Tension=1.5, Combat=0.5, Eerie=3.
    /// </summary>
    public static IReadOnlyDictionary<MusicLayer, MusicLayerDefinition> All { get; } =
        new Dictionary<MusicLayer, MusicLayerDefinition>
        {
            [MusicLayer.Base] = new(MusicLayer.Base, 0f),
            [MusicLayer.Exploration] = new(MusicLayer.Exploration, 2f),
            [MusicLayer.Tension] = new(MusicLayer.Tension, 1.5f),
            [MusicLayer.Combat] = new(MusicLayer.Combat, 0.5f),
            [MusicLayer.Eerie] = new(MusicLayer.Eerie, 3f),
        };

    /// <summary>
    /// Returns the fade duration for the given layer.
    /// </summary>
    public static float GetFadeDuration(MusicLayer layer)
        => All.TryGetValue(layer, out var def) ? def.FadeDurationSeconds : 0f;
}
```

### 3. MusicLayerResolver.cs

```csharp
using Oravey2.Core.Framework.Events;
using Oravey2.Core.Framework.State;

namespace Oravey2.Core.Audio;

/// <summary>
/// Determines which music layers should be active based on GameState and context flags.
/// Pure logic — the Stride MusicStateProcessor calls Resolve() and handles actual crossfading.
/// </summary>
public sealed class MusicLayerResolver
{
    private readonly IEventBus _eventBus;
    private readonly HashSet<MusicLayer> _activeLayers = new() { MusicLayer.Base };

    /// <summary>Currently active music layers.</summary>
    public IReadOnlySet<MusicLayer> ActiveLayers => _activeLayers;

    public MusicLayerResolver(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Evaluates which layers should be active and publishes activation/deactivation events
    /// for any changes.
    /// </summary>
    /// <param name="state">Current game state.</param>
    /// <param name="enemiesNearby">True if non-aggro enemies are within detection range.</param>
    /// <param name="inIrradiatedZone">True if the player is in a zone with radiation > 0.</param>
    public void Resolve(GameState state, bool enemiesNearby, bool inIrradiatedZone)
    {
        var desired = new HashSet<MusicLayer> { MusicLayer.Base };

        if (state == GameState.Exploring)
            desired.Add(MusicLayer.Exploration);

        if (state == GameState.InCombat)
            desired.Add(MusicLayer.Combat);

        if (enemiesNearby && state != GameState.InCombat)
            desired.Add(MusicLayer.Tension);

        if (inIrradiatedZone)
            desired.Add(MusicLayer.Eerie);

        // Deactivate layers no longer desired
        foreach (var layer in _activeLayers)
        {
            if (!desired.Contains(layer))
            {
                float fade = MusicLayerDefinition.GetFadeDuration(layer);
                _eventBus.Publish(new MusicLayerDeactivatedEvent(layer, fade));
            }
        }

        // Activate newly desired layers
        foreach (var layer in desired)
        {
            if (!_activeLayers.Contains(layer))
            {
                float fade = MusicLayerDefinition.GetFadeDuration(layer);
                _eventBus.Publish(new MusicLayerActivatedEvent(layer, fade));
            }
        }

        _activeLayers.Clear();
        foreach (var layer in desired)
            _activeLayers.Add(layer);
    }
}
```

### 4. WeatherState.cs

```csharp
namespace Oravey2.Core.Audio;

public enum WeatherState
{
    Clear,
    Foggy,
    DustStorm,
    AcidRain
}
```

### 5. WeatherProcessor.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Audio;

/// <summary>
/// Manages random weather transitions with configurable timing and weighted probabilities.
/// Pure logic — Stride scripts read Current and apply VFX/audio accordingly.
/// </summary>
public sealed class WeatherProcessor
{
    public const float DefaultMinDuration = 120f;  // 2 min real
    public const float DefaultMaxDuration = 600f;  // 10 min real

    private static readonly Dictionary<WeatherState, (WeatherState state, int weight)[]> TransitionWeights = new()
    {
        [WeatherState.Clear] = [
            (WeatherState.Clear, 50), (WeatherState.Foggy, 25),
            (WeatherState.DustStorm, 15), (WeatherState.AcidRain, 10)
        ],
        [WeatherState.Foggy] = [
            (WeatherState.Clear, 45), (WeatherState.Foggy, 25),
            (WeatherState.DustStorm, 15), (WeatherState.AcidRain, 15)
        ],
        [WeatherState.DustStorm] = [
            (WeatherState.Clear, 40), (WeatherState.Foggy, 25),
            (WeatherState.DustStorm, 20), (WeatherState.AcidRain, 15)
        ],
        [WeatherState.AcidRain] = [
            (WeatherState.Clear, 40), (WeatherState.Foggy, 25),
            (WeatherState.DustStorm, 15), (WeatherState.AcidRain, 20)
        ],
    };

    private readonly IEventBus _eventBus;
    private readonly Random _random;
    private readonly float _minDuration;
    private readonly float _maxDuration;

    private WeatherState _current;
    private float _timeRemaining;

    /// <summary>Current weather state.</summary>
    public WeatherState Current => _current;

    /// <summary>Seconds remaining before the next weather transition check.</summary>
    public float TimeRemaining => _timeRemaining;

    public WeatherProcessor(IEventBus eventBus,
        float minDuration = DefaultMinDuration,
        float maxDuration = DefaultMaxDuration,
        Random? random = null)
    {
        _eventBus = eventBus;
        _minDuration = minDuration > 0 ? minDuration : DefaultMinDuration;
        _maxDuration = maxDuration > minDuration ? maxDuration : minDuration + 1f;
        _random = random ?? new Random();
        _current = WeatherState.Clear;
        _timeRemaining = NextDuration();
    }

    /// <summary>
    /// Advances the weather timer. When the timer expires, picks a new weather state
    /// using weighted random selection and publishes WeatherChangedEvent if it differs.
    /// </summary>
    public void Tick(float deltaSec)
    {
        if (deltaSec <= 0) return;

        _timeRemaining -= deltaSec;
        if (_timeRemaining <= 0)
        {
            var next = PickNextState();
            if (next != _current)
            {
                var old = _current;
                _current = next;
                _eventBus.Publish(new WeatherChangedEvent(old, next));
            }
            _timeRemaining = NextDuration();
        }
    }

    /// <summary>
    /// Forces an immediate weather transition. Publishes WeatherChangedEvent if the state differs.
    /// Resets the timer.
    /// </summary>
    public void ForceWeather(WeatherState state)
    {
        if (state == _current) return;
        var old = _current;
        _current = state;
        _timeRemaining = NextDuration();
        _eventBus.Publish(new WeatherChangedEvent(old, state));
    }

    private float NextDuration()
        => _minDuration + (float)_random.NextDouble() * (_maxDuration - _minDuration);

    private WeatherState PickNextState()
    {
        var weights = TransitionWeights[_current];
        int total = 0;
        foreach (var (_, w) in weights) total += w;

        int roll = _random.Next(total);
        int cumulative = 0;
        foreach (var (state, weight) in weights)
        {
            cumulative += weight;
            if (roll < cumulative) return state;
        }
        return WeatherState.Clear;
    }
}
```

### 6. VolumeSettings.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.Audio;

/// <summary>
/// Manages per-category audio volume levels with a master multiplier.
/// Pure logic — Stride audio service reads effective volumes and applies them.
/// </summary>
public sealed class VolumeSettings
{
    private readonly IEventBus _eventBus;
    private readonly Dictionary<AudioCategory, float> _volumes = new();

    public VolumeSettings(IEventBus eventBus)
    {
        _eventBus = eventBus;
        foreach (AudioCategory cat in Enum.GetValues<AudioCategory>())
            _volumes[cat] = 1f;
    }

    /// <summary>
    /// Gets the raw volume for a category (0–1).
    /// </summary>
    public float GetVolume(AudioCategory category) => _volumes[category];

    /// <summary>
    /// Sets the volume for a category (clamped 0–1). Publishes VolumeChangedEvent if changed.
    /// </summary>
    public void SetVolume(AudioCategory category, float volume)
    {
        float clamped = Math.Clamp(volume, 0f, 1f);
        float old = _volumes[category];
        if (Math.Abs(clamped - old) < 0.0001f) return;

        _volumes[category] = clamped;
        _eventBus.Publish(new VolumeChangedEvent(category, old, clamped));
    }

    /// <summary>
    /// Gets the effective volume for a category: category volume × master volume.
    /// For Master itself, returns the raw master value.
    /// </summary>
    public float GetEffectiveVolume(AudioCategory category)
    {
        if (category == AudioCategory.Master)
            return _volumes[AudioCategory.Master];

        return _volumes[category] * _volumes[AudioCategory.Master];
    }
}
```

### 7. FootstepSurfaceMap.cs

```csharp
using Oravey2.Core.World;

namespace Oravey2.Core.Audio;

/// <summary>
/// Maps TileType to a footstep SFX identifier. Returns null for non-walkable tiles.
/// </summary>
public static class FootstepSurfaceMap
{
    private static readonly Dictionary<TileType, string> Map = new()
    {
        [TileType.Ground] = "sfx_footstep_ground",
        [TileType.Road] = "sfx_footstep_road",
        [TileType.Rubble] = "sfx_footstep_rubble",
        [TileType.Water] = "sfx_footstep_water",
    };

    /// <summary>
    /// Returns the footstep SFX id for the tile type, or null if no footstep
    /// should play (e.g., Empty, Wall).
    /// </summary>
    public static string? GetSfxId(TileType tileType)
        => Map.TryGetValue(tileType, out var id) ? id : null;
}
```

### 8. AmbientZoneMap.cs

```csharp
using Oravey2.Core.World;

namespace Oravey2.Core.Audio;

/// <summary>
/// Maps BiomeType to ambient sound identifiers. Each biome may have multiple ambient loops
/// that play simultaneously (e.g., wind + distant thunder).
/// </summary>
public static class AmbientZoneMap
{
    private static readonly Dictionary<BiomeType, string[]> Map = new()
    {
        [BiomeType.RuinedCity] = ["amb_wind_urban", "amb_creaking_metal"],
        [BiomeType.Wasteland] = ["amb_wind_open", "amb_distant_thunder"],
        [BiomeType.Bunker] = ["amb_hum_mechanical", "amb_dripping_water"],
        [BiomeType.Settlement] = ["amb_crowd_murmur", "amb_campfire_crackle"],
        [BiomeType.Industrial] = ["amb_machinery_hum", "amb_steam_hiss"],
        [BiomeType.ForestOvergrown] = ["amb_wind_leaves", "amb_insects"],
        [BiomeType.IrradiatedCrater] = ["amb_geiger_crackle", "amb_eerie_hum"],
        [BiomeType.Underground] = ["amb_cave_echo", "amb_dripping_water"],
        [BiomeType.Coastal] = ["amb_waves", "amb_seabirds"],
    };

    /// <summary>
    /// Returns the ambient sound IDs for the given biome. Returns an empty array for unknown biomes.
    /// </summary>
    public static string[] GetAmbientIds(BiomeType biome)
        => Map.TryGetValue(biome, out var ids) ? ids : [];
}
```

---

## Test Tables

### MusicLayerResolverTests (~12 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | Base layer active by default | ActiveLayers contains only Base |
| 2 | Exploring activates Exploration | ActiveLayers = {Base, Exploration} |
| 3 | InCombat activates Combat | ActiveLayers = {Base, Combat} |
| 4 | Enemies nearby activates Tension | ActiveLayers = {Base, Exploration, Tension} |
| 5 | Tension not active during combat | enemiesNearby=true, InCombat → no Tension |
| 6 | Irradiated zone activates Eerie | ActiveLayers includes Eerie |
| 7 | Combat + irradiated zone | ActiveLayers = {Base, Combat, Eerie} |
| 8 | All flags active in Exploring | ActiveLayers = {Base, Exploration, Tension, Eerie} |
| 9 | Transition from combat to exploring | Combat deactivated, Exploration activated |
| 10 | Deactivation event has correct fade | MusicLayerDeactivatedEvent.FadeDuration matches spec |
| 11 | Activation event has correct fade | MusicLayerActivatedEvent.FadeDuration matches spec |
| 12 | No events when state unchanged | Resolve same state twice → no new events |

### WeatherProcessorTests (~12 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | Initial state is Clear | Current == Clear |
| 2 | Tick before timer expires no change | Current unchanged, no event |
| 3 | Tick past timer triggers transition | Current may change, timer resets |
| 4 | WeatherChangedEvent published on transition | Event received with correct Old/New |
| 5 | No event when random picks same state | Event not published |
| 6 | ForceWeather changes state | Current == forced state |
| 7 | ForceWeather publishes event | WeatherChangedEvent with correct states |
| 8 | ForceWeather same state is no-op | No event, timer unchanged |
| 9 | Negative delta ignored | Tick(-1) → no change |
| 10 | Zero delta ignored | Tick(0) → no change |
| 11 | Timer resets after transition | TimeRemaining > 0 after transition |
| 12 | Multiple ticks accumulate | Time decreases across ticks |

### VolumeSettingsTests (~10 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | Default volumes are 1.0 | All categories return 1.0 |
| 2 | SetVolume stores value | GetVolume returns new value |
| 3 | SetVolume clamps above 1 | GetVolume == 1.0 |
| 4 | SetVolume clamps below 0 | GetVolume == 0.0 |
| 5 | Effective volume is category × master | SetMaster(0.5), SetSFX(0.8) → 0.4 |
| 6 | Effective master returns raw | GetEffectiveVolume(Master) == raw master |
| 7 | VolumeChangedEvent on change | Event fired with old and new values |
| 8 | No event when same value | SetVolume same → no event |
| 9 | Multiple categories independent | Changing SFX doesn't affect Music |
| 10 | Master 0 mutes all effective | All GetEffectiveVolume return 0 |

### FootstepSurfaceMapTests (~6 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | Ground → sfx_footstep_ground | Correct SFX id |
| 2 | Road → sfx_footstep_road | Correct SFX id |
| 3 | Rubble → sfx_footstep_rubble | Correct SFX id |
| 4 | Water → sfx_footstep_water | Correct SFX id |
| 5 | Empty → null | No footstep for empty |
| 6 | Wall → null | No footstep for wall |

### AmbientZoneMapTests (~10 tests)

| # | Test | Assert |
|---|------|--------|
| 1 | RuinedCity returns 2 ambient ids | Contains wind_urban + creaking_metal |
| 2 | Wasteland returns 2 ids | Contains wind_open + distant_thunder |
| 3 | Bunker returns 2 ids | Contains hum_mechanical + dripping_water |
| 4 | Settlement returns 2 ids | Contains crowd_murmur + campfire_crackle |
| 5 | Industrial returns 2 ids | Contains machinery_hum + steam_hiss |
| 6 | ForestOvergrown returns 2 ids | Contains wind_leaves + insects |
| 7 | IrradiatedCrater returns 2 ids | Contains geiger_crackle + eerie_hum |
| 8 | Underground returns 2 ids | Contains cave_echo + dripping_water |
| 9 | Coastal returns 2 ids | Contains waves + seabirds |
| 10 | Unknown biome returns empty | Hypothetical unknown → [] |

---

## Execution Order

1. Create `Audio/AudioCategory.cs` — no dependencies
2. Create `Audio/MusicLayer.cs` — no dependencies
3. Create `Audio/WeatherState.cs` — no dependencies
4. Create `Audio/FootstepSurfaceMap.cs` — depends on World.TileType
5. Create `Audio/AmbientZoneMap.cs` — depends on World.BiomeType
6. Add events to `GameEvents.cs` — depends on AudioCategory, MusicLayer, WeatherState
7. Create `Audio/MusicLayerResolver.cs` — depends on IEventBus, GameState, MusicLayerDefinition, events
8. Create `Audio/WeatherProcessor.cs` — depends on IEventBus, WeatherState, events
9. Create `Audio/VolumeSettings.cs` — depends on IEventBus, AudioCategory, events
10. Create all test files
11. Run tests — expect ~50 passing
