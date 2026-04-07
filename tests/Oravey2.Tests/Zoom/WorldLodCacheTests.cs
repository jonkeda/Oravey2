using Oravey2.Core.World;

namespace Oravey2.Tests.Zoom;

public class WorldLodCacheTests
{
    private static WorldMapData CreateWorldWithChunk(int cx, int cy, SurfaceType surface,
        byte heightLevel = 5, TileFlags flags = TileFlags.Walkable,
        LiquidType liquid = LiquidType.None, byte waterLevel = 0)
    {
        var world = new WorldMapData(8, 8);
        var tiles = new TileMapData(ChunkData.Size, ChunkData.Size);

        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                tiles.SetTileData(x, y, new TileData(
                    Surface: surface,
                    HeightLevel: heightLevel,
                    WaterLevel: waterLevel,
                    StructureId: 0,
                    Flags: flags,
                    VariantSeed: 0,
                    Liquid: liquid));

        var chunk = new ChunkData(cx, cy, tiles);
        world.SetChunk(cx, cy, chunk);
        return world;
    }

    [Fact]
    public void BiomeDerivation_DominantGrass_IsGrassland()
    {
        var world = CreateWorldWithChunk(0, 0, SurfaceType.Grass);
        var cache = new WorldLodCache(world);

        var cell = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Grassland, cell.Biome);
    }

    [Fact]
    public void BiomeDerivation_DominantConcrete_IsUrban()
    {
        var world = CreateWorldWithChunk(0, 0, SurfaceType.Concrete);
        var cache = new WorldLodCache(world);

        var cell = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Urban, cell.Biome);
    }

    [Fact]
    public void BiomeDerivation_MixedForest_IsForest()
    {
        // Grass + Forested flag → Forest
        var world = CreateWorldWithChunk(0, 0, SurfaceType.Grass,
            flags: TileFlags.Walkable | TileFlags.Forested);
        var cache = new WorldLodCache(world);

        var cell = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Forest, cell.Biome);
    }

    [Fact]
    public void InvalidateRegion_RecalculatesAffectedCells()
    {
        var world = CreateWorldWithChunk(0, 0, SurfaceType.Grass);
        var cache = new WorldLodCache(world);

        // Prime the cache
        var before = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Grassland, before.Biome);

        // Now mutate the chunk in WorldMapData to be urban
        var urbanTiles = new TileMapData(ChunkData.Size, ChunkData.Size);
        for (int x = 0; x < ChunkData.Size; x++)
            for (int y = 0; y < ChunkData.Size; y++)
                urbanTiles.SetTileData(x, y, new TileData(
                    Surface: SurfaceType.Concrete,
                    HeightLevel: 5,
                    WaterLevel: 0,
                    StructureId: 0,
                    Flags: TileFlags.Walkable,
                    VariantSeed: 0));
        world.SetChunk(0, 0, new ChunkData(0, 0, urbanTiles));

        // Without invalidation, cache returns stale data
        var stale = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Grassland, stale.Biome);

        // After invalidation, cache recomputes
        cache.InvalidateChunk(0, 0);
        var after = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Urban, after.Biome);
    }

    [Fact]
    public void AverageHeight_ComputedCorrectly()
    {
        // All tiles at height 10 → avg = 10 * 0.25 = 2.5
        var world = CreateWorldWithChunk(0, 0, SurfaceType.Dirt, heightLevel: 10);
        var cache = new WorldLodCache(world);

        var cell = cache.GetL2Cell(0, 0);
        Assert.Equal(2.5f, cell.AverageHeight, 0.01f);
    }

    [Fact]
    public void EmptyChunk_ReturnsWasteland()
    {
        var world = new WorldMapData(4, 4);
        var cache = new WorldLodCache(world);

        // No chunk set at (0,0) → null → Wasteland
        var cell = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Wasteland, cell.Biome);
    }

    [Fact]
    public void L3Cell_AveragesL2Cells()
    {
        var world = new WorldMapData(8, 8);

        // Set 4 chunks in the first L3 cell (0..3 x 0..3)
        for (int x = 0; x < 4; x++)
        {
            for (int y = 0; y < 4; y++)
            {
                var tiles = new TileMapData(ChunkData.Size, ChunkData.Size);
                for (int tx = 0; tx < ChunkData.Size; tx++)
                    for (int ty = 0; ty < ChunkData.Size; ty++)
                        tiles.SetTileData(tx, ty, new TileData(
                            Surface: SurfaceType.Grass,
                            HeightLevel: 8,
                            WaterLevel: 0,
                            StructureId: 0,
                            Flags: TileFlags.Walkable,
                            VariantSeed: 0));
                world.SetChunk(x, y, new ChunkData(x, y, tiles));
            }
        }

        var cache = new WorldLodCache(world);
        var l3 = cache.GetL3Cell(0, 0);

        Assert.Equal(LodBiome.Grassland, l3.Biome);
        // Height 8 * 0.25 = 2.0
        Assert.Equal(2.0f, l3.AverageHeight, 0.01f);
    }

    [Fact]
    public void WaterDominated_ReturnsWaterBiome()
    {
        var world = CreateWorldWithChunk(0, 0, SurfaceType.Dirt,
            heightLevel: 0, liquid: LiquidType.Water, waterLevel: 5);
        var cache = new WorldLodCache(world);

        var cell = cache.GetL2Cell(0, 0);
        Assert.Equal(LodBiome.Water, cell.Biome);
    }
}
