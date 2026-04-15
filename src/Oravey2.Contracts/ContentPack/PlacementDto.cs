namespace Oravey2.Contracts.ContentPack;

public sealed class PlacementDto
{
    public int ChunkX { get; set; } = 0;
    public int ChunkY { get; set; } = 0;
    public int LocalTileX { get; set; } = 0;
    public int LocalTileY { get; set; } = 0;

    public PlacementDto() { }
    public PlacementDto(int chunkX, int chunkY, int localTileX, int localTileY)
    {
        ChunkX = chunkX;
        ChunkY = chunkY;
        LocalTileX = localTileX;
        LocalTileY = localTileY;
    }

    public override bool Equals(object? obj) =>
        obj is PlacementDto other &&
        ChunkX == other.ChunkX && ChunkY == other.ChunkY &&
        LocalTileX == other.LocalTileX && LocalTileY == other.LocalTileY;

    public override int GetHashCode() =>
        HashCode.Combine(ChunkX, ChunkY, LocalTileX, LocalTileY);
}
