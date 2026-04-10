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

    [Description("Settlement role, e.g. trading_hub, military_outpost")]
    public string Role { get; set; } = "";

    [Description("Faction name appropriate for the role")]
    public string Faction { get; set; } = "";

    [Description("Threat level (1 = safe, 10 = deadly)")]
    public int ThreatLevel { get; set; }

    [Description("1-2 sentence description of the settlement")]
    public string Description { get; set; } = "";

    // Used by Mode B text fallback only — not exposed in tool schema
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}
