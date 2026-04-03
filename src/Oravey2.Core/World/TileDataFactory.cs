namespace Oravey2.Core.World;

public static class TileDataFactory
{
    public static TileData Ground(byte height = 1, byte variant = 0)
        => new(SurfaceType.Dirt, height, 0, 0, TileFlags.Walkable, variant);

    public static TileData Road(byte height = 1, byte variant = 0)
        => new(SurfaceType.Asphalt, height, 0, 0, TileFlags.Walkable, variant);

    public static TileData Rubble(byte height = 1, byte variant = 0)
        => new(SurfaceType.Rock, height, 0, 0, TileFlags.Walkable, variant);

    public static TileData Water(byte waterLevel = 2, byte terrainHeight = 0, byte variant = 0)
        => new(SurfaceType.Mud, terrainHeight, waterLevel, 0, TileFlags.None, variant);

    public static TileData Wall(byte height = 1, byte variant = 0)
        => new(SurfaceType.Concrete, height, 0, 1, TileFlags.None, variant);

    public static TileData FromLegacy(TileType legacy) => legacy switch
    {
        TileType.Ground => Ground(),
        TileType.Road => Road(),
        TileType.Rubble => Rubble(),
        TileType.Water => Water(),
        TileType.Wall => Wall(),
        _ => TileData.Empty
    };
}
