using System.ComponentModel;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Schema class for LLM tool calls. Properties map to the JSON schema
/// that AIFunctionFactory exposes to the model.
/// </summary>
internal sealed class LlmTownEntry
{
    [Description("A thematic name for the settlement")]
    public string GameName { get; set; } = "";

    [Description("The real-world name of the settlement")]
    public string RealName { get; set; } = "";

    [Description("1-2 sentence description of the settlement")]
    public string Description { get; set; } = "";

    [Description("Settlement size: Hamlet, Village, Town, City, Metropolis")]
    public string Size { get; set; } = "Village";

    [Description("Estimated number of inhabitants")]
    public int Inhabitants { get; set; }

    [Description("Level of destruction: Pristine, Light, Moderate, Heavy, Devastated")]
    public string Destruction { get; set; } = "Moderate";

    // Used by Mode B text fallback only — not exposed in tool schema
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
