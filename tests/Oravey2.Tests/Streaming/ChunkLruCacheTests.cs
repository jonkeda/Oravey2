using Oravey2.Core.World;

namespace Oravey2.Tests.Streaming;

public class ChunkLruCacheTests
{
    [Fact]
    public void Add_ThenGet_ReturnsCachedItem()
    {
        var cache = new ChunkLruCache(capacity: 8);
        var chunk = ChunkData.CreateDefault(3, 4);

        cache.Add((3, 4), chunk);
        var result = cache.Get((3, 4));

        Assert.Same(chunk, result);
    }

    [Fact]
    public void Get_MissingKey_ReturnsNull()
    {
        var cache = new ChunkLruCache(capacity: 8);
        Assert.Null(cache.Get((0, 0)));
    }

    [Fact]
    public void EvictionOrder_LeastRecentlyUsed()
    {
        var cache = new ChunkLruCache(capacity: 3);

        cache.Add((0, 0), ChunkData.CreateDefault(0, 0));
        cache.Add((1, 1), ChunkData.CreateDefault(1, 1));
        cache.Add((2, 2), ChunkData.CreateDefault(2, 2));

        // Access (0,0) to make it most-recently-used
        cache.Get((0, 0));

        // Adding a 4th item should evict (1,1) — the least recently used
        cache.Add((3, 3), ChunkData.CreateDefault(3, 3));

        Assert.NotNull(cache.Get((0, 0))); // recently accessed
        Assert.Null(cache.Get((1, 1)));     // evicted
        Assert.NotNull(cache.Get((2, 2))); // still present
        Assert.NotNull(cache.Get((3, 3))); // just added
    }

    [Fact]
    public void Capacity_Respected()
    {
        var cache = new ChunkLruCache(capacity: 2);

        cache.Add((0, 0), ChunkData.CreateDefault(0, 0));
        cache.Add((1, 1), ChunkData.CreateDefault(1, 1));
        cache.Add((2, 2), ChunkData.CreateDefault(2, 2)); // evicts (0,0)

        Assert.Equal(2, cache.Count);
        Assert.Null(cache.Get((0, 0)));
        Assert.NotNull(cache.Get((1, 1)));
        Assert.NotNull(cache.Get((2, 2)));
    }

    [Fact]
    public void Get_UpdatesAccessTime()
    {
        var cache = new ChunkLruCache(capacity: 2);

        cache.Add((0, 0), ChunkData.CreateDefault(0, 0));
        cache.Add((1, 1), ChunkData.CreateDefault(1, 1));

        // Access (0,0) to promote it — (1,1) is now LRU
        cache.Get((0, 0));

        // Adding a 3rd should evict (1,1), not (0,0)
        cache.Add((2, 2), ChunkData.CreateDefault(2, 2));

        Assert.NotNull(cache.Get((0, 0))); // promoted, survived
        Assert.Null(cache.Get((1, 1)));     // evicted
        Assert.NotNull(cache.Get((2, 2)));
    }

    [Fact]
    public void Invalidate_RemovesEntry()
    {
        var cache = new ChunkLruCache(capacity: 8);

        cache.Add((5, 5), ChunkData.CreateDefault(5, 5));
        Assert.True(cache.Contains((5, 5)));

        cache.Invalidate((5, 5));

        Assert.False(cache.Contains((5, 5)));
        Assert.Null(cache.Get((5, 5)));
        Assert.Equal(0, cache.Count);
    }

    [Fact]
    public void Invalidate_NonExistent_NoError()
    {
        var cache = new ChunkLruCache(capacity: 8);
        cache.Invalidate((99, 99)); // should not throw
    }

    [Fact]
    public void Clear_RemovesAllEntries()
    {
        var cache = new ChunkLruCache(capacity: 8);
        cache.Add((0, 0), ChunkData.CreateDefault(0, 0));
        cache.Add((1, 1), ChunkData.CreateDefault(1, 1));

        cache.Clear();

        Assert.Equal(0, cache.Count);
        Assert.Null(cache.Get((0, 0)));
        Assert.Null(cache.Get((1, 1)));
    }

    [Fact]
    public void Add_SameKey_UpdatesValue()
    {
        var cache = new ChunkLruCache(capacity: 8);
        var chunk1 = ChunkData.CreateDefault(1, 1);
        var chunk2 = ChunkData.CreateDefault(1, 1);

        cache.Add((1, 1), chunk1);
        cache.Add((1, 1), chunk2);

        Assert.Equal(1, cache.Count);
        Assert.Same(chunk2, cache.Get((1, 1)));
    }
}
