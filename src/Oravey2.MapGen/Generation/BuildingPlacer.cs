namespace Oravey2.MapGen.Generation;

/// <summary>
/// Converts spatial building placements (world coordinates) to rasterized footprints on a game grid.
/// Handles rotation, boundary clamping, and collision detection.
/// </summary>
public sealed class BuildingPlacer
{
    /// <summary>
    /// Rasterizes a building placement into a footprint of tile coordinates.
    /// </summary>
    /// <param name="placement">The building placement in world tile coordinates</param>
    /// <param name="gridWidth">Width of the terrain grid in tiles</param>
    /// <param name="gridHeight">Height of the terrain grid in tiles</param>
    /// <returns>A 2D array representing the building footprint (relative tile coordinates)</returns>
    public static int[][] RasterizeBuilding(
        WorldTilePlacement placement,
        int gridWidth,
        int gridHeight)
    {
        var footprint = new List<(int TileX, int TileZ)>();
        
        // Generate unrotated footprint
        var unrotated = GenerateUnrotatedFootprint(placement.WidthTiles, placement.DepthTiles);
        
        // Apply rotation
        var rotated = ApplyRotation(unrotated, placement.RotationDegrees);
        
        // Translate to world coordinates and clamp to grid bounds
        foreach (var (localX, localZ) in rotated)
        {
            int worldTileX = placement.CenterX + localX;
            int worldTileZ = placement.CenterZ + localZ;
            
            // Clamp to grid bounds
            if (worldTileX >= 0 && worldTileX < gridWidth &&
                worldTileZ >= 0 && worldTileZ < gridHeight)
            {
                footprint.Add((worldTileX, worldTileZ));
            }
        }
        
        // Convert to 2D array format (each row: [tileX, tileZ])
        var result = new int[footprint.Count][];
        for (int i = 0; i < footprint.Count; i++)
        {
            result[i] = new[] { footprint[i].TileX, footprint[i].TileZ };
        }
        
        return result;
    }
    
    /// <summary>
    /// Applies a building footprint to the terrain array, setting tiles to Building type.
    /// </summary>
    /// <param name="terrain">The terrain array (tile type IDs)</param>
    /// <param name="gridWidth">Width of the terrain grid</param>
    /// <param name="gridHeight">Height of the terrain grid</param>
    /// <param name="footprint">The building footprint to apply</param>
    /// <returns>The number of collisions (non-grass tiles overwritten)</returns>
    public static int ApplyBuildingToSurface(
        int[][] terrain,
        int gridWidth,
        int gridHeight,
        int[][] footprint)
    {
        int collisionCount = 0;
        
        foreach (var tile in footprint)
        {
            if (tile.Length < 2)
                continue;
            
            int tileX = tile[0];
            int tileZ = tile[1];
            
            if (tileX < 0 || tileX >= gridWidth || tileZ < 0 || tileZ >= gridHeight)
                continue;
            
            // Check for collision (non-grass tile)
            if (terrain[tileZ][tileX] != 0) // 0 = Grass
            {
                collisionCount++;
            }
            
            // Apply building type (3)
            terrain[tileZ][tileX] = 3;
        }
        
        return collisionCount;
    }
    
    /// <summary>
    /// Tests if a building can be placed without collision.
    /// </summary>
    /// <param name="terrain">The terrain array (tile type IDs)</param>
    /// <param name="gridWidth">Width of the terrain grid</param>
    /// <param name="gridHeight">Height of the terrain grid</param>
    /// <param name="footprint">The building footprint to test</param>
    /// <returns>A tuple of (canPlace: bool, collisionCount: int)</returns>
    public static (bool CanPlace, int CollisionCount) CanPlaceBuilding(
        int[][] terrain,
        int gridWidth,
        int gridHeight,
        int[][] footprint)
    {
        int collisionCount = 0;
        
        foreach (var tile in footprint)
        {
            if (tile.Length < 2)
                continue;
            
            int tileX = tile[0];
            int tileZ = tile[1];
            
            // Out of bounds is a collision
            if (tileX < 0 || tileX >= gridWidth || tileZ < 0 || tileZ >= gridHeight)
            {
                collisionCount++;
                continue;
            }
            
            // Check against Road (1), Water (2), and existing Buildings (3)
            int tileType = terrain[tileZ][tileX];
            if (tileType != 0) // 0 = Grass (always placeable)
            {
                collisionCount++;
            }
        }
        
        // Can place only if no collisions
        return (collisionCount == 0, collisionCount);
    }
    
    /// <summary>
    /// Generates the unrotated footprint (relative coordinates centered at origin).
    /// </summary>
    private static List<(int LocalX, int LocalZ)> GenerateUnrotatedFootprint(
        int widthTiles,
        int depthTiles)
    {
        var footprint = new List<(int, int)>();
        
        // Generate rectangle centered at (0, 0)
        int halfWidth = widthTiles / 2;
        int halfDepth = depthTiles / 2;
        
        for (int x = -halfWidth; x < widthTiles - halfWidth; x++)
        {
            for (int z = -halfDepth; z < depthTiles - halfDepth; z++)
            {
                footprint.Add((x, z));
            }
        }
        
        return footprint;
    }
    
    /// <summary>
    /// Applies 2D rotation to coordinates around the origin.
    /// Supports 8 rotation angles (0°, 45°, 90°, 135°, 180°, 225°, 270°, 315°).
    /// </summary>
    private static List<(int LocalX, int LocalZ)> ApplyRotation(
        List<(int LocalX, int LocalZ)> coords,
        double rotationDegrees)
    {
        // Normalize rotation to 0-360 range
        rotationDegrees = ((rotationDegrees % 360) + 360) % 360;
        
        // For 45° increments, use discrete rotations to avoid floating-point errors
        int rotationStep = (int)Math.Round(rotationDegrees / 45.0);
        
        var result = new List<(int, int)>();
        
        foreach (var (x, z) in coords)
        {
            var (newX, newZ) = RotatePoint(x, z, rotationStep);
            result.Add((newX, newZ));
        }
        
        return result;
    }
    
    /// <summary>
    /// Rotates a single point around the origin by a multiple of 45°.
    /// </summary>
    /// <param name="x">X coordinate</param>
    /// <param name="z">Z coordinate</param>
    /// <param name="rotationStep">Number of 45° increments (0-7)</param>
    /// <returns>Rotated (x, z) coordinates</returns>
    private static (int X, int Z) RotatePoint(int x, int z, int rotationStep)
    {
        // Normalize to 0-7 range
        rotationStep = ((rotationStep % 8) + 8) % 8;
        
        return rotationStep switch
        {
            0 => (x, z),                          // 0°
            1 => ApplyDiagonalRotation(x, z, 1), // 45°
            2 => (-z, x),                         // 90°
            3 => ApplyDiagonalRotation(x, z, 3), // 135°
            4 => (-x, -z),                        // 180°
            5 => ApplyDiagonalRotation(x, z, 5), // 225°
            6 => (z, -x),                         // 270°
            7 => ApplyDiagonalRotation(x, z, 7), // 315°
            _ => (x, z)
        };
    }
    
    /// <summary>
    /// Applies diagonal rotation (45°, 135°, 225°, 315°) using approximate rotation matrix.
    /// </summary>
    private static (int X, int Z) ApplyDiagonalRotation(int x, int z, int rotationStep)
    {
        // For diagonal rotations, use approximate rotation (rounding to nearest integer)
        // This prevents excessive coordinate drift due to floating-point errors
        
        double angle = rotationStep * 45.0 * Math.PI / 180.0;
        double cos = Math.Cos(angle);
        double sin = Math.Sin(angle);
        
        double newX = x * cos - z * sin;
        double newZ = x * sin + z * cos;
        
        return ((int)Math.Round(newX), (int)Math.Round(newZ));
    }
}
