using Oravey2.Core.Data;
using Oravey2.Core.Framework.Events;

namespace Oravey2.Core.World;

/// <summary>
/// Provides a chunk to the streaming processor when it is not available in the
/// data provider. Implementations call the appropriate generator and persist
/// the result via <see cref="WorldMapStore"/>.
/// </summary>
public interface IChunkGenerator
{
    /// <summary>
    /// Generates a chunk at the given grid position and returns it.
    /// The implementation is responsible for persisting the result.
    /// </summary>
    ChunkData Generate(int chunkX, int chunkY);
}

/// <summary>
/// Calculates which chunks should be loaded/unloaded based on the player's current
/// chunk position. Maintains a configurable active grid (default 5×5) centered on
/// the player chunk. Reads chunk data from <see cref="MapDataProvider"/> and
/// triggers on-demand generation for missing chunks.
/// 
/// This is the pure-logic layer. The Stride SyncScript wrapper calls Update()
/// each frame and handles actual entity spawning/despawning.
/// </summary>
public sealed class ChunkStreamingProcessor
{
    public const int DefaultGridRadius = 2; // 5×5 = radius 2 around center

    private readonly WorldMapData _world;
    private readonly IEventBus _eventBus;
    private readonly MapDataProvider? _dataProvider;
    private readonly IChunkGenerator? _generator;
    private readonly ChunkLruCache? _lruCache;

    private int _activeGridRadius;
    private int _currentCenterX = -1;
    private int _currentCenterY = -1;

    private readonly HashSet<(int cx, int cy)> _loadedChunks = new();

    public int ActiveGridRadius => _activeGridRadius;
    public int CurrentCenterX => _currentCenterX;
    public int CurrentCenterY => _currentCenterY;
    public IReadOnlySet<(int cx, int cy)> LoadedChunks => _loadedChunks;

    /// <summary>
    /// Creates a streaming processor with full dependency injection.
    /// </summary>
    /// <param name="world">The in-memory world grid for bounds checking and chunk storage.</param>
    /// <param name="eventBus">Event bus for load/unload notifications.</param>
    /// <param name="dataProvider">SQLite data source for chunk reads. Null for legacy in-memory mode.</param>
    /// <param name="generator">On-demand generator for missing chunks. Null to skip generation.</param>
    /// <param name="lruCache">LRU cache for recently unloaded chunks. Null to disable caching.</param>
    /// <param name="gridRadius">Active grid radius. 1 = 3×3, 2 = 5×5 (default).</param>
    public ChunkStreamingProcessor(
        WorldMapData world,
        IEventBus eventBus,
        MapDataProvider? dataProvider = null,
        IChunkGenerator? generator = null,
        ChunkLruCache? lruCache = null,
        int gridRadius = DefaultGridRadius)
    {
        if (gridRadius < 1)
            throw new ArgumentOutOfRangeException(nameof(gridRadius), "Grid radius must be at least 1.");

        _world = world;
        _eventBus = eventBus;
        _dataProvider = dataProvider;
        _generator = generator;
        _lruCache = lruCache;
        _activeGridRadius = gridRadius;
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

        // Calculate desired grid
        var desired = new HashSet<(int cx, int cy)>();
        for (int dx = -_activeGridRadius; dx <= _activeGridRadius; dx++)
        {
            for (int dy = -_activeGridRadius; dy <= _activeGridRadius; dy++)
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
                // Move to LRU cache instead of immediate disposal
                if (_lruCache is not null)
                {
                    var chunkData = _world.GetChunk(chunk.cx, chunk.cy);
                    if (chunkData is not null)
                        _lruCache.Add(chunk, chunkData);
                }

                unloaded.Add(chunk);
                _eventBus.Publish(new ChunkUnloadedEvent(chunk.cx, chunk.cy));
            }
        }

        // Load newly required chunks
        foreach (var chunk in desired)
        {
            if (!_loadedChunks.Contains(chunk))
            {
                EnsureChunkLoaded(chunk.cx, chunk.cy);
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
        // Unload everything currently loaded (bypass cache on force-load)
        foreach (var chunk in _loadedChunks)
            _eventBus.Publish(new ChunkUnloadedEvent(chunk.cx, chunk.cy));
        _loadedChunks.Clear();

        _currentCenterX = -1;
        _currentCenterY = -1;

        var (loaded, _) = Update(playerChunkX, playerChunkY);
        return loaded;
    }

    /// <summary>
    /// Ensures a chunk is present in <see cref="WorldMapData"/>. Checks (in order):
    /// 1. Already in WorldMapData → done.
    /// 2. LRU cache hit → restore to WorldMapData.
    /// 3. MapDataProvider (SQLite) → load into WorldMapData.
    /// 4. IChunkGenerator → generate, persist, load into WorldMapData.
    /// 5. Fallback → create a default (flat) placeholder chunk.
    /// </summary>
    private void EnsureChunkLoaded(int cx, int cy)
    {
        // 1. Already loaded in memory
        if (_world.GetChunk(cx, cy) is not null)
            return;

        // 2. Check LRU cache
        if (_lruCache is not null)
        {
            var cached = _lruCache.Get((cx, cy));
            if (cached is not null)
            {
                _world.SetChunk(cx, cy, cached);
                return;
            }
        }

        // 3. Load from SQLite via MapDataProvider
        if (_dataProvider is not null)
        {
            // Convention: regionId 1 for now (single-region worlds)
            var chunkData = _dataProvider.GetChunkData(1, cx, cy);
            if (chunkData is not null)
            {
                _world.SetChunk(cx, cy, chunkData);
                return;
            }
        }

        // 4. On-demand generation
        if (_generator is not null)
        {
            var generated = _generator.Generate(cx, cy);
            _world.SetChunk(cx, cy, generated);
            return;
        }

        // 5. Fallback: create default placeholder
        _world.SetChunk(cx, cy, ChunkData.CreateDefault(cx, cy));
    }
}
