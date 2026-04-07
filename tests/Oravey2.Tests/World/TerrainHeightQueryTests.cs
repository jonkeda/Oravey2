using Oravey2.Core.World;
using Oravey2.Core.World.Terrain;

namespace Oravey2.Tests.World;

public class TerrainHeightQueryTests
{
    /// <summary>
    /// Creates a flat map where all tiles have the same height.
    /// </summary>
    private static TileMapData CreateFlatMap(int size, byte heightLevel = 0)
    {
        var map = new TileMapData(size, size);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
                map.SetTileData(x, y, new TileData(
                    SurfaceType.Dirt, heightLevel, 0, 0, TileFlags.Walkable, 0));
        return map;
    }

    /// <summary>
    /// Creates a map with a cliff: left half at height 0, right half at height 10.
    /// </summary>
    private static TileMapData CreateCliffMap(int size)
    {
        var map = new TileMapData(size, size);
        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                byte h = (byte)(x >= size / 2 ? 10 : 0);
                map.SetTileData(x, y, new TileData(
                    SurfaceType.Dirt, h, 0, 0, TileFlags.Walkable, 0));
            }
        return map;
    }

    /// <summary>
    /// Creates a map with deep water at a specific tile.
    /// </summary>
    private static TileMapData CreateDeepWaterMap(int size, int waterX, int waterY, byte depth)
    {
        var map = CreateFlatMap(size, 0);
        map.SetTileData(waterX, waterY, new TileData(
            SurfaceType.Dirt, 0, depth, 0, TileFlags.Walkable, 0, LiquidType.Water));
        return map;
    }

    [Fact]
    public void GetHeight_FlatMap_ReturnsZero()
    {
        var map = CreateFlatMap(16);
        var query = new TerrainHeightQuery(map);

        float h = query.GetHeight(0, 0);

        Assert.Equal(0f, h, 0.01f);
    }

    [Fact]
    public void GetHeight_ElevatedFlatMap_ReturnsConsistentHeight()
    {
        var map = CreateFlatMap(16, heightLevel: 4);
        var query = new TerrainHeightQuery(map);

        float h1 = query.GetHeight(0, 0);
        float h2 = query.GetHeight(2, 2);

        // Both should be the same height on a flat map
        Assert.Equal(h1, h2, 0.01f);
        Assert.True(h1 > 0, "Elevated terrain should have positive height");
    }

    [Fact]
    public void GetTileAt_Origin_ReturnsCenterTile()
    {
        var map = CreateFlatMap(16);
        var query = new TerrainHeightQuery(map);

        // World origin (0,0) maps to the center of the map
        var tile = query.GetTileAt(0, 0);

        Assert.Equal(SurfaceType.Dirt, tile.Surface);
        Assert.True(tile.IsWalkable);
    }

    [Fact]
    public void GetTileAt_OutOfBounds_ReturnsDefault()
    {
        var map = CreateFlatMap(16);
        var query = new TerrainHeightQuery(map);

        // Way outside the map
        var tile = query.GetTileAt(1000, 1000);

        Assert.Equal(default(TileData), tile);
    }

    [Fact]
    public void IsCliffBlocking_SameTile_ReturnsFalse()
    {
        var map = CreateCliffMap(16);
        var query = new TerrainHeightQuery(map);

        // Same position — never blocked
        bool blocked = query.IsCliffBlocking(0, 0, 0, 0);

        Assert.False(blocked);
    }

    [Fact]
    public void IsCliffBlocking_CrossingCliff_ReturnsTrue()
    {
        var map = CreateCliffMap(32);
        var query = new TerrainHeightQuery(map, HeightmapMeshGenerator.TileWorldSize);

        // Move from tile in left half (height 0) to tile in right half (height 10)
        // Half the map is at x=0 in world space (since centered), so tiles at x<16 are left, x>=16 right
        // World x for tile 14 = 14*2 - 32 = -4, tile 16 = 16*2 - 32 = 0
        float tileSize = HeightmapMeshGenerator.TileWorldSize;
        float halfWorldX = 32 * tileSize / 2f;
        float fromX = 14 * tileSize - halfWorldX + tileSize / 2; // tile 14, left half
        float toX = 16 * tileSize - halfWorldX + tileSize / 2;   // tile 16, right half

        bool blocked = query.IsCliffBlocking(fromX, 0, toX, 0);

        Assert.True(blocked);
    }

    [Fact]
    public void IsCliffBlocking_GentleSlope_ReturnsFalse()
    {
        // Create a map with gentle slope: height increases by 1 per tile
        var map = new TileMapData(16, 16);
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                map.SetTileData(x, y, new TileData(
                    SurfaceType.Dirt, (byte)x, 0, 0, TileFlags.Walkable, 0));

        var query = new TerrainHeightQuery(map);
        float tileSize = HeightmapMeshGenerator.TileWorldSize;
        float halfWorld = 16 * tileSize / 2f;

        // Adjacent tiles with delta of 1 — gentle slope, should be passable
        float fromX = 5 * tileSize - halfWorld + tileSize / 2;
        float toX = 6 * tileSize - halfWorld + tileSize / 2;

        bool blocked = query.IsCliffBlocking(fromX, 0, toX, 0);

        Assert.False(blocked);
    }

    [Fact]
    public void IsDeepLiquid_NoWater_ReturnsFalse()
    {
        var map = CreateFlatMap(16);
        var query = new TerrainHeightQuery(map);

        Assert.False(query.IsDeepLiquid(0, 0));
    }

    [Fact]
    public void IsDeepLiquid_ShallowWater_ReturnsFalse()
    {
        // WaterDepth = WaterLevel - HeightLevel = 2 - 0 = 2, which equals MaxWadeDepth
        var map = CreateDeepWaterMap(16, 8, 8, depth: 2);
        var query = new TerrainHeightQuery(map);

        float tileSize = HeightmapMeshGenerator.TileWorldSize;
        float halfWorld = 16 * tileSize / 2f;
        float wx = 8 * tileSize - halfWorld + tileSize / 2;
        float wz = 8 * tileSize - halfWorld + tileSize / 2;

        Assert.False(query.IsDeepLiquid(wx, wz));
    }

    [Fact]
    public void IsDeepLiquid_DeepWater_ReturnsTrue()
    {
        // WaterDepth = 5 - 0 = 5, which exceeds MaxWadeDepth (2)
        var map = CreateDeepWaterMap(16, 8, 8, depth: 5);
        var query = new TerrainHeightQuery(map);

        float tileSize = HeightmapMeshGenerator.TileWorldSize;
        float halfWorld = 16 * tileSize / 2f;
        float wx = 8 * tileSize - halfWorld + tileSize / 2;
        float wz = 8 * tileSize - halfWorld + tileSize / 2;

        Assert.True(query.IsDeepLiquid(wx, wz));
    }

    [Fact]
    public void GetEffectiveHeight_NoWater_ReturnsTerrainHeight()
    {
        var map = CreateFlatMap(16, heightLevel: 4);
        var query = new TerrainHeightQuery(map);

        float effective = query.GetEffectiveHeight(0, 0);
        float terrain = query.GetHeight(0, 0);

        Assert.Equal(terrain, effective, 0.01f);
    }

    [Fact]
    public void GetEffectiveHeight_ShallowWater_ReturnsWaterSurface()
    {
        // Height 0, water level 2 → shallow (depth 2 = MaxWadeDepth)
        var map = new TileMapData(16, 16);
        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                map.SetTileData(x, y, new TileData(
                    SurfaceType.Dirt, 0, 2, 0, TileFlags.Walkable, 0, LiquidType.Water));

        var query = new TerrainHeightQuery(map);

        float effective = query.GetEffectiveHeight(0, 0);
        float expectedWaterY = 2 * HeightmapMeshGenerator.HeightStep;

        Assert.True(effective >= expectedWaterY - 0.01f,
            $"Effective height {effective} should be at least water surface {expectedWaterY}");
    }

    [Fact]
    public void Constants_HaveExpectedValues()
    {
        Assert.Equal(0.5f, TerrainHeightQuery.PlayerHeightOffset);
        Assert.Equal(15f, TerrainHeightQuery.HeightSmoothing);
        Assert.Equal(3f, TerrainHeightQuery.SnapThreshold);
        Assert.Equal(2, TerrainHeightQuery.MaxWadeDepth);
    }

    [Fact]
    public void GetHeight_CachesChunkBuilds()
    {
        var map = CreateFlatMap(32);
        var query = new TerrainHeightQuery(map);

        // First call builds the chunk
        float h1 = query.GetHeight(0, 0);
        // Second call should use cache (same result)
        float h2 = query.GetHeight(1, 1);

        // Both are on flat terrain, should be similar
        Assert.Equal(h1, h2, 0.1f);
    }

    [Fact]
    public void IsCliffBlocking_OutOfBounds_ReturnsTrue()
    {
        var map = CreateFlatMap(16);
        var query = new TerrainHeightQuery(map);

        // Moving far out of bounds
        bool blocked = query.IsCliffBlocking(0, 0, 1000, 1000);

        Assert.True(blocked);
    }
}
