namespace Oravey2.MapGen.Spatial;

public sealed record BuildingFootprint(
    string Id,
    int TileX,
    int TileY,
    int Width,
    int Height)
{
    public bool Overlaps(BuildingFootprint other)
    {
        return TileX < other.TileX + other.Width &&
               TileX + Width > other.TileX &&
               TileY < other.TileY + other.Height &&
               TileY + Height > other.TileY;
    }

    public IEnumerable<(int X, int Y)> OccupiedTiles()
    {
        for (int dx = 0; dx < Width; dx++)
            for (int dy = 0; dy < Height; dy++)
                yield return (TileX + dx, TileY + dy);
    }
}
