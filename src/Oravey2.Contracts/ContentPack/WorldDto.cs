namespace Oravey2.Contracts.ContentPack;

public sealed class WorldDto
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string Source { get; set; } = "";
    public int ChunksWide { get; set; } = 0;
    public int ChunksHigh { get; set; } = 0;
    public int TileSize { get; set; } = 0;
    public PlacementDto? PlayerStart { get; set; } = null;
    public List<TownRefDto> Towns { get; set; } = [];
}

public sealed class TownRefDto
{
    public string GameName { get; set; } = "";
    public string RealName { get; set; } = "";
    public float GameX { get; set; } = 0f;
    public float GameY { get; set; } = 0f;
    public string Description { get; set; } = "";
    public string Size { get; set; } = "";
    public int Inhabitants { get; set; } = 0;
    public string Destruction { get; set; } = "";
}
