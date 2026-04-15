using System.ComponentModel;

namespace Oravey2.Contracts.ContentPack;

public sealed class CuratedTownDto
{
    [Description("A thematic name for the settlement")]
    public string GameName { get; set; } = "";

    [Description("The real-world name of the settlement")]
    public string RealName { get; set; } = "";

    public double Latitude { get; set; } = 0.0;
    public double Longitude { get; set; } = 0.0;

    [Description("1-2 sentence description of the settlement")]
    public string Description { get; set; } = "";

    [Description("Settlement size: Hamlet, Village, Town, City, Metropolis")]
    public string Size { get; set; } = "Village";

    [Description("Estimated number of inhabitants")]
    public int Inhabitants { get; set; } = 0;

    [Description("Level of destruction: Pristine, Light, Moderate, Heavy, Devastated")]
    public string Destruction { get; set; } = "Moderate";
}
