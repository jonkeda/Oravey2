# Design: Step 07 — World Streaming

Implements chunk-based world loading, zone definitions, day/night cycle, and fast travel per [docs/steps/07-world-streaming.md](../steps/07-world-streaming.md). Constants from [GAME_CONSTANTS.md](../constants/GAME_CONSTANTS.md) §7. Event flows from [EVENT_FLOWS.md](../events/EVENT_FLOWS.md) §5, §7. Data models follow [docs/schemas/world.md](../schemas/world.md) and [docs/schemas/zones.md](../schemas/zones.md).

**Depends on:** Step 1 (TileMapData, TileType, EventBus, IEventBus, IGameEvent)

---

## Deferred to Stride Integration

The following require Stride runtime and are NOT implemented in this step:

- `ChunkStreamingScript` (SyncScript) — actual entity spawning/despawning per chunk
- `TileMapRendererScript` chunk rendering — already exists as stub
- `DayNightCycleScript` (SyncScript) — adjusts light direction/colour per phase
- `ZoneTriggerComponent` (EntityComponent) — spatial trigger volumes
- Nav-mesh generation/stitching per chunk
- Minimap / fog-of-war rendering

---

## File Layout

All new files go in `src/Oravey2.Core/`. Tests in `tests/Oravey2.Tests/`.

```
src/Oravey2.Core/
├── World/
│   ├── TileType.cs                # existing
│   ├── TileMapData.cs             # existing
│   ├── BiomeType.cs               # enum
│   ├── DayPhase.cs                # enum (Dawn, Day, Dusk, Night)
│   ├── ChunkData.cs               # chunk tile data + entity spawn info + modified state
│   ├── EntitySpawnInfo.cs          # record — what to spawn when chunk loads
│   ├── WorldMapData.cs            # grid of chunk references
│   ├── ZoneDefinition.cs          # record — zone metadata
│   ├── ZoneRegistry.cs            # zone lookup by chunk coords
│   ├── ChunkStreamingProcessor.cs # calculates load/unload sets from player position
│   ├── DayNightCycleProcessor.cs  # advances in-game time, tracks phase transitions
│   ├── FastTravelService.cs       # discovered locations, travel validation, time cost
│   └── DiscoveredLocation.cs      # record
├── Framework/
│   └── Events/
│       └── GameEvents.cs          # add new events (existing file)
tests/Oravey2.Tests/
├── World/
│   ├── TileMapDataTests.cs        # existing
│   ├── ChunkDataTests.cs
│   ├── WorldMapDataTests.cs
│   ├── ZoneRegistryTests.cs
│   ├── ChunkStreamingProcessorTests.cs
│   ├── DayNightCycleProcessorTests.cs
│   └── FastTravelServiceTests.cs
```

**Source files:** 10 new + 1 modified (GameEvents.cs)
**Test files:** 6 new
**Estimated tests:** ~70

---

## Events to Add to GameEvents.cs

```csharp
// Add to using block:
using Oravey2.Core.World;

// New events:
public readonly record struct ChunkLoadedEvent(int ChunkX, int ChunkY) : IGameEvent;
public readonly record struct ChunkUnloadedEvent(int ChunkX, int ChunkY) : IGameEvent;
public readonly record struct ZoneEnteredEvent(string ZoneId, string ZoneName) : IGameEvent;
public readonly record struct DayPhaseChangedEvent(DayPhase OldPhase, DayPhase NewPhase) : IGameEvent;
public readonly record struct FastTravelEvent(string FromId, string ToId, float InGameHoursCost) : IGameEvent;
public readonly record struct LocationDiscoveredEvent(string LocationId, string LocationName) : IGameEvent;
```

---

## Source Code

### 1. BiomeType.cs

```csharp
namespace Oravey2.Core.World;

public enum BiomeType
{
    RuinedCity,
    Wasteland,
    Bunker,
    Settlement,
    Industrial,
    ForestOvergrown,
    IrradiatedCrater,
    Underground,
    Coastal
}
```

### 2. DayPhase.cs

```csharp
namespace Oravey2.Core.World;

public enum DayPhase
{
    Dawn,
    Day,
    Dusk,
    Night
}
```

### 3. EntitySpawnInfo.cs

```csharp
namespace Oravey2.Core.World;

/// <summary>
/// Describes an entity to spawn when a chunk loads.
/// Position is local to the chunk (0–15 range per axis).
/// </summary>
public sealed record EntitySpawnInfo(
    string PrefabId,
    float LocalX,
    float LocalZ,
    float RotationY = 0f,
    string? Faction = null,
    int? Level = null,
    string? DialogueId = null,
    string? LootTable = null,
    bool Persistent = false,
    string? ConditionFlag = null
);
```

### 4. ChunkData.cs

```csharp
namespace Oravey2.Core.World;

public sealed class ChunkData
{
    public const int Size = 16;

    public int ChunkX { get; }
    public int ChunkY { get; }
    public TileMapData Tiles { get; }
    public IReadOnlyList<EntitySpawnInfo> Entities { get; }

    /// <summary>
    /// Tracks runtime modifications: destroyed objects, looted containers, dead persistent NPCs.
    /// Key = entity/container id, Value = true if modified (destroyed/looted).
    /// </summary>
    public Dictionary<string, bool> ModifiedState { get; } = new();

    public ChunkData(int chunkX, int chunkY, TileMapData? tiles = null,
        IReadOnlyList<EntitySpawnInfo>? entities = null)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        Tiles = tiles ?? new TileMapData(Size, Size);
        Entities = entities ?? Array.Empty<EntitySpawnInfo>();
    }

    /// <summary>
    /// Returns the world-space tile X origin for this chunk.
    /// </summary>
    public int WorldTileX => ChunkX * Size;

    /// <summary>
    /// Returns the world-space tile Y origin for this chunk.
    /// </summary>
    public int WorldTileY => ChunkY * Size;

    /// <summary>
    /// Gets a tile using world-space coordinates. Returns Empty if out of chunk bounds.
    /// </summary>
    public TileType GetWorldTile(int worldX, int worldY)
    {
        int localX = worldX - WorldTileX;
        int localY = worldY - WorldTileY;
        return Tiles.GetTile(localX, localY);
    }

    /// <summary>
    /// Marks an entity or container as modified (looted/destroyed).
    /// </summary>
    public void MarkModified(string entityId)
        => ModifiedState[entityId] = true;

    /// <summary>
    /// Checks whether an entity/container has been modified.
    /// </summary>
    public bool IsModified(string entityId)
        => ModifiedState.TryGetValue(entityId, out var modified) && modified;

    /// <summary>
    /// Creates a default chunk filled with ground tiles and no entities.
    /// </summary>
    public static ChunkData CreateDefault(int chunkX, int chunkY)
    {
        var tiles = new TileMapData(Size, Size);
        for (int x = 0; x < Size; x++)
            for (int y = 0; y < Size; y++)
                tiles.SetTile(x, y, TileType.Ground);
        return new ChunkData(chunkX, chunkY, tiles);
    }
}
```

### 5. WorldMapData.cs

```csharp
namespace Oravey2.Core.World;

public sealed class WorldMapData
{
    public int ChunksWide { get; }
    public int ChunksHigh { get; }

    private readonly ChunkData?[,] _chunks;

    public WorldMapData(int chunksWide, int chunksHigh)
    {
        if (chunksWide <= 0) throw new ArgumentOutOfRangeException(nameof(chunksWide));
        if (chunksHigh <= 0) throw new ArgumentOutOfRangeException(nameof(chunksHigh));

        ChunksWide = chunksWide;
        ChunksHigh = chunksHigh;
        _chunks = new ChunkData?[chunksWide, chunksHigh];
    }

    /// <summary>
    /// Gets the chunk at the given chunk coordinates, or null if out of bounds / not loaded.
    /// </summary>
    public ChunkData? GetChunk(int cx, int cy)
    {
        if (cx < 0 || cx >= ChunksWide || cy < 0 || cy >= ChunksHigh)
            return null;
        return _chunks[cx, cy];
    }

    /// <summary>
    /// Sets a chunk at the given chunk coordinates. Used during world setup and streaming.
    /// </summary>
    public void SetChunk(int cx, int cy, ChunkData? chunk)
    {
        if (cx < 0 || cx >= ChunksWide || cy < 0 || cy >= ChunksHigh)
            return;
        _chunks[cx, cy] = chunk;
    }

    /// <summary>
    /// Converts world-space tile coordinates to chunk coordinates.
    /// </summary>
    public static (int cx, int cy) TileToChunk(int tileX, int tileY)
    {
        // Integer division floors for positive values. For negative, use Math.DivRem wasn't needed
        // since chunks are always non-negative.
        int cx = tileX / ChunkData.Size;
        int cy = tileY / ChunkData.Size;
        return (cx, cy);
    }

    /// <summary>
    /// Checks if chunk coordinates are within the world grid.
    /// </summary>
    public bool InBounds(int cx, int cy)
        => cx >= 0 && cx < ChunksWide && cy >= 0 && cy < ChunksHigh;

    /// <summary>
    /// Returns all non-null chunks currently set. Useful for serialization.
    /// </summary>
    public IEnumerable<ChunkData> GetAllChunks()
    {
        for (int x = 0; x < ChunksWide; x++)
            for (int y = 0; y < ChunksHigh; y++)
                if (_chunks[x, y] is { } chunk)
                    yield return chunk;
    }
}
```

### 6. ZoneDefinition.cs

```csharp
namespace Oravey2.Core.World;

/// <summary>
/// Defines a named zone with biome, radiation, difficulty, and chunk range.
/// </summary>
public sealed record ZoneDefinition(
    string Id,
    string Name,
    BiomeType Biome,
    float RadiationLevel,
    int EnemyDifficultyTier,
    bool IsFastTravelTarget,
    int ChunkStartX,
    int ChunkStartY,
    int ChunkEndX,
    int ChunkEndY
)
{
    /// <summary>
    /// Checks whether the given chunk coordinates fall within this zone's chunk range (inclusive).
    /// </summary>
    public bool ContainsChunk(int cx, int cy)
        => cx >= ChunkStartX && cx <= ChunkEndX
        && cy >= ChunkStartY && cy <= ChunkEndY;
}
```

### 7. ZoneRegistry.cs

```csharp
namespace Oravey2.Core.World;

/// <summary>
/// Stores all zone definitions and provides lookup by chunk coordinates.
/// </summary>
public sealed class ZoneRegistry
{
    private readonly List<ZoneDefinition> _zones = new();

    public IReadOnlyList<ZoneDefinition> Zones => _zones;

    public void Register(ZoneDefinition zone)
    {
        if (_zones.Any(z => z.Id == zone.Id))
            throw new InvalidOperationException($"Zone '{zone.Id}' is already registered.");
        _zones.Add(zone);
    }

    /// <summary>
    /// Finds the zone that contains the given chunk coordinates, or null if no zone matches.
    /// </summary>
    public ZoneDefinition? GetZoneForChunk(int cx, int cy)
        => _zones.FirstOrDefault(z => z.ContainsChunk(cx, cy));

    /// <summary>
    /// Gets a zone by its ID, or null if not found.
    /// </summary>
    public ZoneDefinition? GetZone(string zoneId)
        => _zones.FirstOrDefault(z => z.Id == zoneId);

    /// <summary>
    /// Returns all zones that are valid fast-travel targets.
    /// </summary>
    public IEnumerable<ZoneDefinition> GetFastTravelZones()
        => _zones.Where(z => z.IsFastTravelTarget);
}
```

### 8. DiscoveredLocation.cs

```csharp
namespace Oravey2.Core.World;

/// <summary>
/// A location the player has discovered and can potentially fast-travel to.
/// </summary>
public sealed record DiscoveredLocation(
    string Id,
    string Name,
    int ChunkX,
    int ChunkY
);
```

### 9. ChunkStreamingProcessor.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.World;

/// <summary>
/// Calculates which chunks should be loaded/unloaded based on the player's current
/// chunk position. Maintains a 3×3 active grid centered on the player chunk.
/// 
/// This is the pure-logic layer. The Stride SyncScript wrapper calls Update()
/// each frame and handles actual entity spawning/despawning.
/// </summary>
public sealed class ChunkStreamingProcessor
{
    public const int ActiveGridRadius = 1; // 3×3 = radius 1 around center

    private readonly WorldMapData _world;
    private readonly IEventBus _eventBus;

    private int _currentCenterX = -1;
    private int _currentCenterY = -1;

    private readonly HashSet<(int cx, int cy)> _loadedChunks = new();

    public int CurrentCenterX => _currentCenterX;
    public int CurrentCenterY => _currentCenterY;
    public IReadOnlySet<(int cx, int cy)> LoadedChunks => _loadedChunks;

    public ChunkStreamingProcessor(WorldMapData world, IEventBus eventBus)
    {
        _world = world;
        _eventBus = eventBus;
    }

    /// <summary>
    /// Updates the active chunk grid based on the player's current chunk position.
    /// Returns the set of newly loaded chunks and newly unloaded chunks.
    /// If the player hasn't changed chunks, both sets are empty.
    /// </summary>
    public (IReadOnlyList<(int cx, int cy)> loaded, IReadOnlyList<(int cx, int cy)> unloaded)
        Update(int playerChunkX, int playerChunkY)
    {
        var loaded = new List<(int, int)>();
        var unloaded = new List<(int, int)>();

        if (playerChunkX == _currentCenterX && playerChunkY == _currentCenterY)
            return (loaded, unloaded);

        _currentCenterX = playerChunkX;
        _currentCenterY = playerChunkY;

        // Calculate desired 3×3 grid
        var desired = new HashSet<(int cx, int cy)>();
        for (int dx = -ActiveGridRadius; dx <= ActiveGridRadius; dx++)
        {
            for (int dy = -ActiveGridRadius; dy <= ActiveGridRadius; dy++)
            {
                int cx = playerChunkX + dx;
                int cy = playerChunkY + dy;
                if (_world.InBounds(cx, cy))
                    desired.Add((cx, cy));
            }
        }

        // Unload chunks no longer in the active grid
        foreach (var chunk in _loadedChunks)
        {
            if (!desired.Contains(chunk))
            {
                unloaded.Add(chunk);
                _eventBus.Publish(new ChunkUnloadedEvent(chunk.cx, chunk.cy));
            }
        }

        // Load newly required chunks
        foreach (var chunk in desired)
        {
            if (!_loadedChunks.Contains(chunk))
            {
                loaded.Add(chunk);
                _eventBus.Publish(new ChunkLoadedEvent(chunk.cx, chunk.cy));
            }
        }

        // Update tracked state
        _loadedChunks.Clear();
        foreach (var chunk in desired)
            _loadedChunks.Add(chunk);

        return (loaded, unloaded);
    }

    /// <summary>
    /// Forces a full reload of the active grid. Useful after fast-travel or load-game.
    /// </summary>
    public IReadOnlyList<(int cx, int cy)> ForceLoad(int playerChunkX, int playerChunkY)
    {
        // Unload everything currently loaded
        foreach (var chunk in _loadedChunks)
            _eventBus.Publish(new ChunkUnloadedEvent(chunk.cx, chunk.cy));
        _loadedChunks.Clear();

        _currentCenterX = -1;
        _currentCenterY = -1;

        var (loaded, _) = Update(playerChunkX, playerChunkY);
        return loaded;
    }
}
```

### 10. DayNightCycleProcessor.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.World;

/// <summary>
/// Advances in-game time and tracks day phase transitions.
/// Pure logic — no Stride dependencies. A SyncScript wrapper calls Tick() each frame.
/// </summary>
public sealed class DayNightCycleProcessor
{
    // Phase boundaries (in-game hours)
    public const float DawnStart = 6.0f;
    public const float DayStart = 7.0f;
    public const float DuskStart = 20.0f;
    public const float NightStart = 21.0f;

    // Timing
    public const float DefaultRealSecondsPerHour = 120f; // 48 min real = 24h game

    private readonly IEventBus _eventBus;

    private float _inGameHour;
    private DayPhase _currentPhase;
    private float _realSecondsPerHour;

    /// <summary>Current in-game hour (0.0–24.0, wraps).</summary>
    public float InGameHour => _inGameHour;

    /// <summary>Current day phase.</summary>
    public DayPhase CurrentPhase => _currentPhase;

    /// <summary>Real seconds per in-game hour. Default 120 (48 min real = full day).</summary>
    public float RealSecondsPerHour
    {
        get => _realSecondsPerHour;
        set => _realSecondsPerHour = value > 0 ? value : DefaultRealSecondsPerHour;
    }

    public DayNightCycleProcessor(IEventBus eventBus, float startHour = 8.0f,
        float realSecondsPerHour = DefaultRealSecondsPerHour)
    {
        _eventBus = eventBus;
        _realSecondsPerHour = realSecondsPerHour;
        _inGameHour = Math.Clamp(startHour, 0f, 24f);
        _currentPhase = GetPhase(_inGameHour);
    }

    /// <summary>
    /// Advances time by the given real-time delta (in seconds).
    /// Publishes DayPhaseChangedEvent when crossing a boundary.
    /// </summary>
    public void Tick(float realDeltaSeconds)
    {
        if (realDeltaSeconds <= 0) return;

        float hoursElapsed = realDeltaSeconds / _realSecondsPerHour;
        _inGameHour += hoursElapsed;

        // Wrap around midnight
        while (_inGameHour >= 24f)
            _inGameHour -= 24f;

        var newPhase = GetPhase(_inGameHour);
        if (newPhase != _currentPhase)
        {
            var old = _currentPhase;
            _currentPhase = newPhase;
            _eventBus.Publish(new DayPhaseChangedEvent(old, newPhase));
        }
    }

    /// <summary>
    /// Advances time by a given number of in-game hours. Used for fast-travel, sleeping, etc.
    /// May fire multiple phase-change events if crossing several boundaries.
    /// </summary>
    public void AdvanceHours(float inGameHours)
    {
        if (inGameHours <= 0) return;

        // Step in small increments to detect each phase boundary
        const float step = 0.25f; // 15-minute steps
        float remaining = inGameHours;

        while (remaining > 0)
        {
            float advance = Math.Min(remaining, step);
            float realEquivalent = advance * _realSecondsPerHour;
            Tick(realEquivalent);
            remaining -= advance;
        }
    }

    /// <summary>
    /// Sets in-game time directly. Publishes phase change if applicable.
    /// </summary>
    public void SetTime(float hour)
    {
        _inGameHour = Math.Clamp(hour, 0f, 24f);
        var newPhase = GetPhase(_inGameHour);
        if (newPhase != _currentPhase)
        {
            var old = _currentPhase;
            _currentPhase = newPhase;
            _eventBus.Publish(new DayPhaseChangedEvent(old, newPhase));
        }
    }

    /// <summary>
    /// Determines the day phase from an in-game hour.
    /// </summary>
    public static DayPhase GetPhase(float hour)
    {
        return hour switch
        {
            >= DawnStart and < DayStart => DayPhase.Dawn,
            >= DayStart and < DuskStart => DayPhase.Day,
            >= DuskStart and < NightStart => DayPhase.Dusk,
            _ => DayPhase.Night
        };
    }
}
```

### 11. FastTravelService.cs

```csharp
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.World;

/// <summary>
/// Manages discovered fast-travel locations, validates travel, and calculates time costs.
/// </summary>
public sealed class FastTravelService
{
    /// <summary>Distance divisor to get in-game hours cost.</summary>
    public const float TravelTimeDivisor = 10f;

    private readonly Dictionary<string, DiscoveredLocation> _locations = new();
    private readonly IEventBus _eventBus;

    public IReadOnlyList<DiscoveredLocation> Locations
        => _locations.Values.ToList().AsReadOnly();

    public FastTravelService(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    /// <summary>
    /// Discovers a new location. Publishes LocationDiscoveredEvent if new.
    /// </summary>
    public bool Discover(DiscoveredLocation location)
    {
        if (_locations.ContainsKey(location.Id))
            return false;

        _locations[location.Id] = location;
        _eventBus.Publish(new LocationDiscoveredEvent(location.Id, location.Name));
        return true;
    }

    /// <summary>
    /// Checks if a location has been discovered.
    /// </summary>
    public bool IsDiscovered(string locationId)
        => _locations.ContainsKey(locationId);

    /// <summary>
    /// Gets a discovered location by ID, or null.
    /// </summary>
    public DiscoveredLocation? GetLocation(string locationId)
        => _locations.TryGetValue(locationId, out var loc) ? loc : null;

    /// <summary>
    /// Checks whether the player can travel between two discovered locations.
    /// Both must be discovered.
    /// </summary>
    public bool CanTravel(string fromId, string toId)
    {
        if (fromId == toId) return false;
        return _locations.ContainsKey(fromId) && _locations.ContainsKey(toId);
    }

    /// <summary>
    /// Calculates travel time in in-game hours based on chunk distance.
    /// Formula: Manhattan distance of chunks / TravelTimeDivisor.
    /// Returns -1 if either location is not discovered.
    /// </summary>
    public float GetTravelTime(string fromId, string toId)
    {
        if (!_locations.TryGetValue(fromId, out var from) ||
            !_locations.TryGetValue(toId, out var to))
            return -1f;

        float distance = Math.Abs(to.ChunkX - from.ChunkX) + Math.Abs(to.ChunkY - from.ChunkY);
        return distance / TravelTimeDivisor;
    }

    /// <summary>
    /// Executes fast travel. Publishes FastTravelEvent with the time cost.
    /// Returns the destination location, or null if travel is not possible.
    /// The caller (Stride script) handles actual teleportation and time advance.
    /// </summary>
    public (DiscoveredLocation destination, float hoursCost)? Travel(string fromId, string toId)
    {
        if (!CanTravel(fromId, toId))
            return null;

        var destination = _locations[toId];
        float hours = GetTravelTime(fromId, toId);

        _eventBus.Publish(new FastTravelEvent(fromId, toId, hours));
        return (destination, hours);
    }
}
```

---

## Test Tables

### ChunkDataTests.cs — 8 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Constructor_DefaultTiles_16x16` | `new ChunkData(3, 4)` | `Tiles.Width == 16`, `Tiles.Height == 16` |
| 2 | `ChunkXY_SetFromConstructor` | `new ChunkData(3, 4)` | `ChunkX == 3, ChunkY == 4` |
| 3 | `WorldTileX_ChunkSizeMultiple` | `new ChunkData(2, 3)` | `WorldTileX == 32, WorldTileY == 48` |
| 4 | `GetWorldTile_MapsToLocal` | Create chunk(1,0), set tile(16,5)=Road | `GetWorldTile(16, 5) == Road` |
| 5 | `GetWorldTile_OutOfBounds_Empty` | `new ChunkData(0, 0)` | `GetWorldTile(20, 20) == Empty` |
| 6 | `MarkModified_FlagsEntity` | `MarkModified("npc_01")` | `IsModified("npc_01") == true` |
| 7 | `IsModified_UnknownId_False` | Fresh chunk | `IsModified("xyz") == false` |
| 8 | `CreateDefault_AllGround` | `CreateDefault(1, 1)` | All 16×16 tiles == Ground |

### WorldMapDataTests.cs — 8 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Constructor_InvalidSize_Throws` | `new WorldMapData(0, 5)` | `ArgumentOutOfRangeException` |
| 2 | `SetGet_Chunk_RoundTrip` | Set chunk(2,3) | `GetChunk(2,3)` returns same |
| 3 | `GetChunk_OutOfBounds_Null` | 4×4 map | `GetChunk(10, 10) == null` |
| 4 | `GetChunk_NegativeCoords_Null` | 4×4 map | `GetChunk(-1, 0) == null` |
| 5 | `InBounds_ValidCoords_True` | 4×4 map | `InBounds(3,3) == true` |
| 6 | `InBounds_OutOfRange_False` | 4×4 map | `InBounds(4,0) == false` |
| 7 | `TileToChunk_Conversion` | — | `TileToChunk(33, 17) == (2, 1)` |
| 8 | `GetAllChunks_ReturnsNonNull` | Set 2 chunks | `GetAllChunks().Count() == 2` |

### ZoneRegistryTests.cs — 7 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Register_AddsZone` | Register 1 zone | `Zones.Count == 1` |
| 2 | `Register_DuplicateId_Throws` | Register same ID twice | `InvalidOperationException` |
| 3 | `GetZoneForChunk_InRange_Found` | Zone covers (4,4)→(5,5) | `GetZoneForChunk(4, 5)?.Id == "haven"` |
| 4 | `GetZoneForChunk_OutOfRange_Null` | Same zone | `GetZoneForChunk(0, 0) == null` |
| 5 | `GetZone_ById_Found` | Register "haven" | `GetZone("haven") != null` |
| 6 | `GetZone_UnknownId_Null` | — | `GetZone("nope") == null` |
| 7 | `GetFastTravelZones_FiltersCorrectly` | 2 zones: 1 FT, 1 not | Count == 1 |

### ChunkStreamingProcessorTests.cs — 12 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Update_FirstCall_LoadsGrid` | 8×8 world, update(4,4) | loaded.Count == 9 (3×3) |
| 2 | `Update_SameChunk_NoChange` | Update(4,4) twice | second returns empty lists |
| 3 | `Update_MoveRight_Loads3Unloads3` | Update(4,4) then (5,4) | loaded 3, unloaded 3 |
| 4 | `Update_CornerChunk_ClampedGrid` | 8×8 world, update(0,0) | loaded.Count == 4 (2×2 corner) |
| 5 | `Update_PublishesLoadEvents` | Subscribe to ChunkLoadedEvent | events received == loaded count |
| 6 | `Update_PublishesUnloadEvents` | Move away, subscribe UnloadEvent | events received == unloaded count |
| 7 | `LoadedChunks_TracksCurrentGrid` | Update(4,4) | LoadedChunks.Count == 9 |
| 8 | `ForceLoad_ClearsAndReloads` | Update(4,4), ForceLoad(4,4) | unload events + load events fired |
| 9 | `ForceLoad_DifferentPosition` | Update(4,4), ForceLoad(7,7) | new grid centered on (7,7) |
| 10 | `Update_EdgeMoveLoadsCorrectChunks` | Move from center to edge | specific chunks in loaded list |
| 11 | `CurrentCenter_UpdatesOnMove` | Update(4,4) | `CurrentCenterX == 4, CurrentCenterY == 4` |
| 12 | `Update_SmallWorld_2x2_LimitsGrid` | 2×2 world, update(0,0) | loaded.Count == 4 |

### DayNightCycleProcessorTests.cs — 14 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Constructor_DefaultStartHour8_DayPhase` | `new(bus, 8f)` | `CurrentPhase == Day`, `InGameHour == 8f` |
| 2 | `GetPhase_5_Night` | — | `GetPhase(5f) == Night` |
| 3 | `GetPhase_6_Dawn` | — | `GetPhase(6f) == Dawn` |
| 4 | `GetPhase_7_Day` | — | `GetPhase(7f) == Day` |
| 5 | `GetPhase_20_Dusk` | — | `GetPhase(20f) == Dusk` |
| 6 | `GetPhase_21_Night` | — | `GetPhase(21f) == Night` |
| 7 | `Tick_AdvancesTime` | Start at 8, tick 120s (=1 hour) | `InGameHour ≈ 9.0` |
| 8 | `Tick_PhaseChange_PublishesEvent` | Start at 6.5 (Dawn), tick into Day | `DayPhaseChangedEvent(Dawn, Day)` published |
| 9 | `Tick_NoPhaseChange_NoEvent` | Start at 10, small tick | no event published |
| 10 | `Tick_WrapsAtMidnight` | Start at 23.5, tick 1.5 hours | `InGameHour ≈ 1.0` |
| 11 | `AdvanceHours_SmallStep` | Start at 8, advance 2 hours | `InGameHour ≈ 10.0` |
| 12 | `AdvanceHours_CrossesMultiplePhases` | Start at 6 (Dawn), advance 16h | phase events for Dawn→Day, Day→Dusk, Dusk→Night |
| 13 | `SetTime_ChangesPhase` | Start at 8 (Day), SetTime(22) | `CurrentPhase == Night`, event published |
| 14 | `SetTime_SamePhase_NoEvent` | Start at 10, SetTime(12) | no event published |

### FastTravelServiceTests.cs — 13 tests

| # | Test Name | Setup | Assert |
|---|-----------|-------|--------|
| 1 | `Discover_NewLocation_ReturnsTrue` | Discover "haven" | returns true, Locations.Count == 1 |
| 2 | `Discover_Duplicate_ReturnsFalse` | Discover same twice | second returns false |
| 3 | `Discover_PublishesEvent` | Subscribe LocationDiscoveredEvent | event received |
| 4 | `IsDiscovered_Known_True` | Discover "haven" | `IsDiscovered("haven") == true` |
| 5 | `IsDiscovered_Unknown_False` | — | `IsDiscovered("nope") == false` |
| 6 | `GetLocation_Found` | Discover "haven" | returns matching record |
| 7 | `GetLocation_NotFound_Null` | — | `GetLocation("nope") == null` |
| 8 | `CanTravel_BothDiscovered_True` | Discover A and B | `CanTravel("a", "b") == true` |
| 9 | `CanTravel_SameLocation_False` | Discover A | `CanTravel("a", "a") == false` |
| 10 | `CanTravel_UndiscoveredDest_False` | Discover A only | `CanTravel("a", "b") == false` |
| 11 | `GetTravelTime_ManhattanDistance` | A at (0,0), B at (3,4) | `(3+4)/10 = 0.7` |
| 12 | `Travel_Success_PublishesEvent` | A, B discovered | `FastTravelEvent` published with correct cost |
| 13 | `Travel_CannotTravel_ReturnsNull` | Only A discovered | `Travel("a", "b") == null` |

---

## Execution Order

1. **Create enums:** `BiomeType.cs`, `DayPhase.cs`
2. **Create records:** `EntitySpawnInfo.cs`, `DiscoveredLocation.cs`, `ZoneDefinition.cs`
3. **Create core classes:** `ChunkData.cs`, `WorldMapData.cs`, `ZoneRegistry.cs`
4. **Create processors:** `ChunkStreamingProcessor.cs`, `DayNightCycleProcessor.cs`
5. **Create service:** `FastTravelService.cs`
6. **Modify GameEvents.cs** — add 6 events + `using Oravey2.Core.World`
7. **Build Core** — verify 0 errors
8. **Create tests:** `ChunkDataTests.cs`, `WorldMapDataTests.cs`, `ZoneRegistryTests.cs`, `ChunkStreamingProcessorTests.cs`, `DayNightCycleProcessorTests.cs`, `FastTravelServiceTests.cs`
9. **Run full test suite** — verify all pass (~470 total)
