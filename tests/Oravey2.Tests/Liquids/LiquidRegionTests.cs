using Oravey2.Core.World;
using Oravey2.Core.World.Liquids;

namespace Oravey2.Tests.Liquids;

public class LiquidRegionTests
{
    private static TileMapData CreateMapWithLiquid(
        int width, int height,
        (int x, int y, LiquidType type)[] liquidTiles)
    {
        var map = new TileMapData(width, height);
        for (int x = 0; x < width; x++)
            for (int y = 0; y < height; y++)
                map.SetTileData(x, y, TileDataFactory.Ground(height: 4));

        foreach (var (x, y, type) in liquidTiles)
        {
            map.SetTileData(x, y, new TileData(
                Surface: SurfaceType.Mud, HeightLevel: 1, WaterLevel: 3,
                StructureId: 0, Flags: TileFlags.None, VariantSeed: 0,
                Liquid: type));
        }

        return map;
    }

    [Fact]
    public void ConnectedWaterTiles_GroupedIntoOneRegion()
    {
        // 4 adjacent water tiles in a 2×2 square
        var map = CreateMapWithLiquid(5, 5, [
            (1, 1, LiquidType.Water),
            (2, 1, LiquidType.Water),
            (1, 2, LiquidType.Water),
            (2, 2, LiquidType.Water),
        ]);

        var regions = LiquidRegionFinder.FindRegions(map);

        Assert.Single(regions);
        Assert.Equal(4, regions[0].Tiles.Count);
        Assert.Equal(LiquidType.Water, regions[0].Type);
    }

    [Fact]
    public void DisjointWaterTiles_GroupedIntoTwoRegions()
    {
        // Two separate water bodies
        var map = CreateMapWithLiquid(6, 1, [
            (0, 0, LiquidType.Water),
            (5, 0, LiquidType.Water),
        ]);

        var regions = LiquidRegionFinder.FindRegions(map);

        Assert.Equal(2, regions.Count);
        Assert.All(regions, r => Assert.Single(r.Tiles));
    }

    [Fact]
    public void DifferentLiquidTypes_NotGroupedTogether()
    {
        // Adjacent water + lava → separate regions
        var map = CreateMapWithLiquid(3, 1, [
            (0, 0, LiquidType.Water),
            (1, 0, LiquidType.Lava),
        ]);

        var regions = LiquidRegionFinder.FindRegions(map);

        Assert.Equal(2, regions.Count);
        Assert.Contains(regions, r => r.Type == LiquidType.Water);
        Assert.Contains(regions, r => r.Type == LiquidType.Lava);
    }

    [Fact]
    public void ShoreDetection_IdentifiesEdgeTiles()
    {
        // 3×3 water block surrounded by ground — all tiles are shore
        var map = CreateMapWithLiquid(5, 5, [
            (1, 1, LiquidType.Water),
            (2, 1, LiquidType.Water),
            (3, 1, LiquidType.Water),
            (1, 2, LiquidType.Water),
            (2, 2, LiquidType.Water),
            (3, 2, LiquidType.Water),
            (1, 3, LiquidType.Water),
            (2, 3, LiquidType.Water),
            (3, 3, LiquidType.Water),
        ]);

        var regions = LiquidRegionFinder.FindRegions(map);

        Assert.Single(regions);
        var region = regions[0];

        // All border tiles are shore; centre (2,2) is not (surrounded by water on all 4 sides)
        Assert.Equal(8, region.ShoreTiles.Count);
        Assert.DoesNotContain((2, 2), region.ShoreTiles);
    }
}
