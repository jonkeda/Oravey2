using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class BuildingPlacerTests
{
    private const int TestGridWidth = 100;
    private const int TestGridHeight = 100;
    
    /// <summary>
    /// Creates a test terrain array with all grass tiles (0).
    /// </summary>
    private static int[][] CreateClearTerrain(int width, int height)
    {
        var terrain = new int[height][];
        for (int z = 0; z < height; z++)
        {
            terrain[z] = new int[width];
            for (int x = 0; x < width; x++)
            {
                terrain[z][x] = 0; // Grass
            }
        }
        return terrain;
    }
    
    /// <summary>
    /// Creates a test terrain array with some non-grass tiles.
    /// </summary>
    private static int[][] CreateTerrainWithObstacles(int width, int height)
    {
        var terrain = CreateClearTerrain(width, height);
        
        // Add road (1)
        for (int x = 40; x < 60; x++)
        {
            terrain[50][x] = 2; // Road
        }
        
        // Add water (4)
        for (int x = 70; x < 85; x++)
        {
            for (int z = 20; z < 35; z++)
            {
                terrain[z][x] = 4; // Water
            }
        }
        
        return terrain;
    }

    #region Rotation Tests (8 total)
    
    [Fact]
    public void RasterizeBuilding_Rotation0Degrees_GeneratesUnrotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 4, DepthTiles: 4,
            RotationDegrees: 0.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // Should generate a 4x4 square around center (50, 50)
        Assert.NotEmpty(footprint);
        Assert.Equal(16, footprint.Length); // 4x4 = 16 tiles
    }
    
    [Fact]
    public void RasterizeBuilding_Rotation45Degrees_GeneratesRotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 4, DepthTiles: 4,
            RotationDegrees: 45.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // Should generate rotated footprint
        Assert.NotEmpty(footprint);
        // Rotated 4x4 square should still have approximately 16 tiles after clamping
        Assert.True(footprint.Length >= 12 && footprint.Length <= 20);
    }
    
    [Fact]
    public void RasterizeBuilding_Rotation90Degrees_GeneratesRotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 4, DepthTiles: 6,
            RotationDegrees: 90.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // 90° rotation should swap width and depth
        Assert.NotEmpty(footprint);
        Assert.Equal(24, footprint.Length); // 4x6 = 24 tiles (rotated to 6x4)
    }
    
    [Fact]
    public void RasterizeBuilding_Rotation135Degrees_GeneratesRotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 5, DepthTiles: 5,
            RotationDegrees: 135.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        Assert.NotEmpty(footprint);
        // 135° rotation of square should still have approximately 25 tiles
        Assert.True(footprint.Length >= 20 && footprint.Length <= 30);
    }
    
    [Fact]
    public void RasterizeBuilding_Rotation180Degrees_GeneratesRotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 4, DepthTiles: 6,
            RotationDegrees: 180.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // 180° rotation should produce same tile count as unrotated
        Assert.NotEmpty(footprint);
        Assert.Equal(24, footprint.Length); // 4x6 = 24 tiles
    }
    
    [Fact]
    public void RasterizeBuilding_Rotation225Degrees_GeneratesRotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 5, DepthTiles: 5,
            RotationDegrees: 225.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        Assert.NotEmpty(footprint);
        Assert.True(footprint.Length >= 20 && footprint.Length <= 30);
    }
    
    [Fact]
    public void RasterizeBuilding_Rotation270Degrees_GeneratesRotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 4, DepthTiles: 6,
            RotationDegrees: 270.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // 270° rotation should swap width and depth
        Assert.NotEmpty(footprint);
        Assert.Equal(24, footprint.Length); // 4x6 = 24 tiles (rotated to 6x4)
    }
    
    [Fact]
    public void RasterizeBuilding_Rotation315Degrees_GeneratesRotatedFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: 50,
            WidthTiles: 5, DepthTiles: 5,
            RotationDegrees: 315.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        Assert.NotEmpty(footprint);
        Assert.True(footprint.Length >= 20 && footprint.Length <= 30);
    }

    #endregion

    #region Boundary Clamping Tests (4 total)
    
    [Fact]
    public void RasterizeBuilding_CenterAtGridCorner_ClampsToGridBounds()
    {
        var placement = new WorldTilePlacement(
            CenterX: 1, CenterZ: 1,
            WidthTiles: 6, DepthTiles: 6,
            RotationDegrees: 0.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // Should clamp to grid bounds, so fewer than 36 tiles
        Assert.NotEmpty(footprint);
        Assert.True(footprint.Length <= 36);
    }
    
    [Fact]
    public void RasterizeBuilding_BuildingExceedsMaxX_ClampsToGridBounds()
    {
        var placement = new WorldTilePlacement(
            CenterX: TestGridWidth - 2, CenterZ: 50,
            WidthTiles: 6, DepthTiles: 4,
            RotationDegrees: 0.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // Check that all tiles are within bounds
        foreach (var tile in footprint)
        {
            Assert.True(tile[0] >= 0 && tile[0] < TestGridWidth);
            Assert.True(tile[1] >= 0 && tile[1] < TestGridHeight);
        }
    }
    
    [Fact]
    public void RasterizeBuilding_BuildingExceedsMaxZ_ClampsToGridBounds()
    {
        var placement = new WorldTilePlacement(
            CenterX: 50, CenterZ: TestGridHeight - 2,
            WidthTiles: 4, DepthTiles: 6,
            RotationDegrees: 0.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // Check that all tiles are within bounds
        foreach (var tile in footprint)
        {
            Assert.True(tile[0] >= 0 && tile[0] < TestGridWidth);
            Assert.True(tile[1] >= 0 && tile[1] < TestGridHeight);
        }
    }
    
    [Fact]
    public void RasterizeBuilding_BuildingCompletelyOutOfBounds_ReturnsEmptyFootprint()
    {
        var placement = new WorldTilePlacement(
            CenterX: TestGridWidth + 50, CenterZ: TestGridHeight + 50,
            WidthTiles: 4, DepthTiles: 4,
            RotationDegrees: 0.0, AlignmentHint: "");
        
        var footprint = BuildingPlacer.RasterizeBuilding(placement, TestGridWidth, TestGridHeight);
        
        // Should return empty footprint since building is outside grid
        Assert.Empty(footprint);
    }

    #endregion

    #region Collision Detection Tests (6 total)
    
    [Fact]
    public void CanPlaceBuilding_OnClearTerrain_AllowsPlacement()
    {
        var terrain = CreateClearTerrain(100, 100);
        var footprint = new[]
        {
            new[] { 50, 50 },
            new[] { 51, 50 },
            new[] { 50, 51 },
            new[] { 51, 51 }
        };
        
        var (canPlace, collisionCount) = BuildingPlacer.CanPlaceBuilding(terrain, 100, 100, footprint);
        
        Assert.True(canPlace);
        Assert.Equal(0, collisionCount);
    }
    
    [Fact]
    public void CanPlaceBuilding_OnRoad_PreventPlacement()
    {
        var terrain = CreateTerrainWithObstacles(100, 100);
        // Road is at z=50, x=40-60
        var footprint = new[]
        {
            new[] { 45, 48 },
            new[] { 45, 49 },
            new[] { 45, 50 },
            new[] { 45, 51 }
        };
        
        var (canPlace, collisionCount) = BuildingPlacer.CanPlaceBuilding(terrain, 100, 100, footprint);
        
        Assert.False(canPlace);
        Assert.True(collisionCount > 0);
    }
    
    [Fact]
    public void CanPlaceBuilding_OnWater_PreventPlacement()
    {
        var terrain = CreateTerrainWithObstacles(100, 100);
        // Water is at x=70-85, z=20-35
        var footprint = new[]
        {
            new[] { 75, 25 },
            new[] { 76, 25 },
            new[] { 75, 26 },
            new[] { 76, 26 }
        };
        
        var (canPlace, collisionCount) = BuildingPlacer.CanPlaceBuilding(terrain, 100, 100, footprint);
        
        Assert.False(canPlace);
        Assert.True(collisionCount > 0);
    }
    
    [Fact]
    public void CanPlaceBuilding_PartialCollision_PreventPlacement()
    {
        var terrain = CreateTerrainWithObstacles(100, 100);
        // Mix of clear and road tiles
        var footprint = new[]
        {
            new[] { 45, 48 },
            new[] { 46, 48 },
            new[] { 45, 50 }, // On road
            new[] { 46, 50 }  // On road
        };
        
        var (canPlace, collisionCount) = BuildingPlacer.CanPlaceBuilding(terrain, 100, 100, footprint);
        
        Assert.False(canPlace);
        Assert.Equal(2, collisionCount);
    }
    
    [Fact]
    public void CanPlaceBuilding_OutOfBounds_FailPlacement()
    {
        var terrain = CreateClearTerrain(100, 100);
        var footprint = new[]
        {
            new[] { 50, 50 },
            new[] { -1, 50 }, // Out of bounds
            new[] { 50, 51 },
            new[] { 51, 51 }
        };
        
        var (canPlace, collisionCount) = BuildingPlacer.CanPlaceBuilding(terrain, 100, 100, footprint);
        
        Assert.False(canPlace);
        Assert.Equal(1, collisionCount);
    }
    
    [Fact]
    public void CanPlaceBuilding_WithExistingBuilding_PreventPlacement()
    {
        var terrain = CreateClearTerrain(100, 100);
        
        // Mark some tiles as building (type 3)
        terrain[50][50] = 3;
        terrain[50][51] = 3;
        
        var footprint = new[]
        {
            new[] { 50, 50 }, // Occupied
            new[] { 51, 50 },
            new[] { 50, 51 }, // Occupied
            new[] { 51, 51 }
        };
        
        var (canPlace, collisionCount) = BuildingPlacer.CanPlaceBuilding(terrain, 100, 100, footprint);
        
        Assert.False(canPlace);
        Assert.Equal(2, collisionCount);
    }

    #endregion

    #region Apply to Surface Tests (4 total)
    
    [Fact]
    public void ApplyBuildingToSurface_OnClearTerrain_SetsAllTilesToBuilding()
    {
        var terrain = CreateClearTerrain(100, 100);
        var footprint = new[]
        {
            new[] { 50, 50 },
            new[] { 51, 50 },
            new[] { 50, 51 },
            new[] { 51, 51 }
        };
        
        int collisions = BuildingPlacer.ApplyBuildingToSurface(terrain, 100, 100, footprint);
        
        // Check that tiles are marked as building (3)
        Assert.Equal(3, terrain[50][50]);
        Assert.Equal(3, terrain[50][51]);
        Assert.Equal(3, terrain[51][50]);
        Assert.Equal(3, terrain[51][51]);
        
        // No collisions on clear terrain
        Assert.Equal(0, collisions);
    }
    
    [Fact]
    public void ApplyBuildingToSurface_OnRoad_CountsCollisions()
    {
        var terrain = CreateTerrainWithObstacles(100, 100);
        var footprint = new[]
        {
            new[] { 45, 48 },
            new[] { 45, 49 },
            new[] { 45, 50 }, // On road
            new[] { 45, 51 }
        };
        
        int collisions = BuildingPlacer.ApplyBuildingToSurface(terrain, 100, 100, footprint);
        
        // All tiles should be marked as building (3)
        Assert.Equal(3, terrain[48][45]);
        Assert.Equal(3, terrain[49][45]);
        Assert.Equal(3, terrain[50][45]);
        Assert.Equal(3, terrain[51][45]);
        
        // One collision on road
        Assert.Equal(1, collisions);
    }
    
    [Fact]
    public void ApplyBuildingToSurface_OutOfBounds_IgnoresTiles()
    {
        var terrain = CreateClearTerrain(100, 100);
        var footprint = new[]
        {
            new[] { 50, 50 },
            new[] { -1, 50 }, // Out of bounds
            new[] { 50, -1 }, // Out of bounds
            new[] { 100, 100 } // Out of bounds
        };
        
        int collisions = BuildingPlacer.ApplyBuildingToSurface(terrain, 100, 100, footprint);
        
        // Only valid tile should be marked
        Assert.Equal(3, terrain[50][50]);
        
        // Collisions only count valid, out-of-bounds tiles, so 0
        Assert.Equal(0, collisions);
    }
    
    [Fact]
    public void ApplyBuildingToSurface_InvalidFootprintFormat_SkipsInvalidTiles()
    {
        var terrain = CreateClearTerrain(100, 100);
        var footprint = new[]
        {
            new[] { 50 }, // Invalid (only x, no z)
            new[] { 50, 50 }, // Valid
            new[] { 51, 50, 99 } // Valid (but extra element ignored)
        };
        
        int collisions = BuildingPlacer.ApplyBuildingToSurface(terrain, 100, 100, footprint);
        
        // Valid tiles should be marked
        Assert.Equal(3, terrain[50][50]);
        Assert.Equal(3, terrain[50][51]);
        
        // Collision count should only count valid tiles
        Assert.Equal(0, collisions);
    }

    #endregion
}
