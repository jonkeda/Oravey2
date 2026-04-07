namespace Oravey2.Core.World;

/// <summary>
/// Least-recently-used cache for chunks that have left the active streaming grid.
/// Cache hits avoid SQLite reads and mesh rebuilds. When at capacity, the
/// least-recently-accessed entry is evicted.
/// </summary>
public sealed class ChunkLruCache
{
    public const int DefaultCapacity = 64;

    private readonly int _capacity;
    private readonly Dictionary<(int cx, int cy), LinkedListNode<CacheEntry>> _map = new();
    private readonly LinkedList<CacheEntry> _order = new(); // head = most recently used

    public int Capacity => _capacity;
    public int Count => _map.Count;

    public ChunkLruCache(int capacity = DefaultCapacity)
    {
        if (capacity < 1)
            throw new ArgumentOutOfRangeException(nameof(capacity), "Capacity must be at least 1.");

        _capacity = capacity;
    }

    /// <summary>
    /// Retrieves a cached chunk and promotes it to most-recently-used.
    /// Returns null on cache miss.
    /// </summary>
    public ChunkData? Get((int cx, int cy) key)
    {
        if (!_map.TryGetValue(key, out var node))
            return null;

        // Promote to head (most recently used)
        _order.Remove(node);
        _order.AddFirst(node);

        return node.Value.Data;
    }

    /// <summary>
    /// Adds or updates a chunk in the cache. If at capacity, evicts the
    /// least-recently-used entry first.
    /// </summary>
    public void Add((int cx, int cy) key, ChunkData data)
    {
        if (_map.TryGetValue(key, out var existing))
        {
            // Update existing entry and promote to head
            _order.Remove(existing);
            existing.Value = new CacheEntry(key, data);
            _order.AddFirst(existing);
            return;
        }

        // Evict if at capacity
        if (_map.Count >= _capacity)
            EvictOldest();

        var node = new LinkedListNode<CacheEntry>(new CacheEntry(key, data));
        _order.AddFirst(node);
        _map[key] = node;
    }

    /// <summary>
    /// Explicitly removes a cache entry (e.g., content pack invalidation).
    /// </summary>
    public void Invalidate((int cx, int cy) key)
    {
        if (_map.Remove(key, out var node))
            _order.Remove(node);
    }

    /// <summary>
    /// Clears all cached entries.
    /// </summary>
    public void Clear()
    {
        _map.Clear();
        _order.Clear();
    }

    /// <summary>
    /// Returns true if the cache contains the given key.
    /// Does NOT promote the entry (read-only peek).
    /// </summary>
    public bool Contains((int cx, int cy) key) => _map.ContainsKey(key);

    private void EvictOldest()
    {
        var oldest = _order.Last;
        if (oldest is null) return;

        _order.RemoveLast();
        _map.Remove(oldest.Value.Key);
    }

    private record struct CacheEntry((int cx, int cy) Key, ChunkData Data);
}
