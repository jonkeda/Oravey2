using Oravey2.Core.World;
using Oravey2.Core.World.Blueprint;

namespace Oravey2.Tests.Blueprint;

public class TerrainCompilerTests
{
    [Fact]
    public void FlatBlueprint_AllTilesAtBaseElevation()
    {
        var bp = TestBlueprints.Minimal();
        var grid = TerrainCompiler.Compile(bp);

        // 1 chunk = 16×16
        Assert.Equal(16, grid.GetLength(0));
        Assert.Equal(16, grid.GetLength(1));

        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                Assert.Equal(1, grid[x, y].HeightLevel);
    }

    [Fact]
    public void FlatBlueprint_DefaultSurfaceDirt()
    {
        var bp = TestBlueprints.Minimal();
        var grid = TerrainCompiler.Compile(bp);

        Assert.Equal(SurfaceType.Dirt, grid[0, 0].Surface);
    }

    [Fact]
    public void ElevationRegion_TilesInsideHaveElevatedHeight()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Terrain = new TerrainBlueprint(1,
                new[]
                {
                    new TerrainRegion("hill", "elevation",
                        new[] { new[] {4,4}, new[] {10,4}, new[] {10,10}, new[] {4,10} }, 5, 5)
                },
                Array.Empty<SurfaceRule>())
        };
        var grid = TerrainCompiler.Compile(bp);

        // Tile inside polygon
        Assert.Equal(5, grid[7, 7].HeightLevel);
        // Tile outside polygon
        Assert.Equal(1, grid[0, 0].HeightLevel);
    }

    [Fact]
    public void SurfaceRule_100Percent_AllTilesInRegionHaveSurface()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Terrain = new TerrainBlueprint(1,
                new[]
                {
                    new TerrainRegion("area", "elevation",
                        new[] { new[] {2,2}, new[] {12,2}, new[] {12,12}, new[] {2,12} }, 1, 1)
                },
                new[]
                {
                    new SurfaceRule("area", new[] { new SurfaceAllocation("Asphalt", 100) })
                })
        };
        var grid = TerrainCompiler.Compile(bp);

        // Inside the polygon
        Assert.Equal(SurfaceType.Asphalt, grid[5, 5].Surface);
        Assert.Equal(SurfaceType.Asphalt, grid[8, 8].Surface);
    }

    [Fact]
    public void TwoNonOverlappingRegions_CorrectHeights()
    {
        var bp = TestBlueprints.Minimal() with
        {
            Dimensions = new BlueprintDimensions(2, 1),
            Terrain = new TerrainBlueprint(1,
                new[]
                {
                    new TerrainRegion("low", "elevation",
                        new[] { new[] {2,2}, new[] {6,2}, new[] {6,6}, new[] {2,6} }, 2, 2),
                    new TerrainRegion("high", "elevation",
                        new[] { new[] {20,2}, new[] {26,2}, new[] {26,6}, new[] {20,6} }, 8, 8)
                },
                Array.Empty<SurfaceRule>())
        };
        var grid = TerrainCompiler.Compile(bp);

        Assert.Equal(2, grid[4, 4].HeightLevel);
        Assert.Equal(8, grid[23, 4].HeightLevel);
    }

    [Fact]
    public void VariantSeed_DiffersPerPosition()
    {
        var bp = TestBlueprints.Minimal();
        var grid = TerrainCompiler.Compile(bp);

        // Check a few adjacent tiles — seeds should differ
        var seeds = new HashSet<byte>();
        for (int x = 0; x < 5; x++)
            seeds.Add(grid[x, 0].VariantSeed);
        Assert.True(seeds.Count > 1, "VariantSeed should differ across positions");
    }

    [Fact]
    public void AllTilesWalkable_ByDefault()
    {
        var bp = TestBlueprints.Minimal();
        var grid = TerrainCompiler.Compile(bp);

        for (int x = 0; x < 16; x++)
            for (int y = 0; y < 16; y++)
                Assert.True(grid[x, y].IsWalkable);
    }
}
