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
