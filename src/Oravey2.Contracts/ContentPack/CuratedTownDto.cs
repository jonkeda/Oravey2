namespace Oravey2.Contracts.ContentPack;

public sealed class CuratedTownDto
{
    public string GameName { get; set; } = "";
    public string RealName { get; set; } = "";
    public double Latitude { get; set; } = 0.0;
    public double Longitude { get; set; } = 0.0;
    public string Description { get; set; } = "";
    public string Size { get; set; } = "";
    public int Inhabitants { get; set; } = 0;
    public string Destruction { get; set; } = "";
}
