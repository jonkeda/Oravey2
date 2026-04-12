using System.ComponentModel;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Schema class for the LLM town-design tool call.
/// Properties map to the JSON schema that AIFunctionFactory exposes to the model.
/// </summary>
internal sealed class LlmTownDesignEntry
{
    [Description("Array of landmark buildings")]
    public List<LlmLandmarkEntry> Landmarks { get; set; } = [];

    [Description("Array of key locations in the town")]
    public List<LlmKeyLocationEntry> KeyLocations { get; set; } = [];

    [Description("Layout style: grid, radial, organic, linear, clustered, or compound")]
    public string LayoutStyle { get; set; } = "organic";

    [Description("Array of environmental hazards (0 to 4)")]
    public List<LlmHazardEntry> Hazards { get; set; } = [];
}

internal sealed class LlmLandmarkEntry
{
    [Description("Name of the landmark building (post-apocalyptic rename)")]
    public string Name { get; set; } = "";

    [Description("Visual description for 3D asset generation (exterior only)")]
    public string VisualDescription { get; set; } = "";

    [Description("Size category: small, medium, or large")]
    public string SizeCategory { get; set; } = "large";

    [Description("One sentence: what this building was in reality — real name, style, era, purpose")]
    public string OriginalDescription { get; set; } = "";

    [Description("30–60 word Meshy text-to-3D prompt: materials, damage, style. End with 'low-poly game asset'")]
    public string MeshyPrompt { get; set; } = "";

    [Description("Compass direction + nearby feature relative to town centre (e.g. 'north-east, near the harbour')")]
    public string PositionHint { get; set; } = "";
}

internal sealed class LlmKeyLocationEntry
{
    [Description("Name of the location (post-apocalyptic rename)")]
    public string Name { get; set; } = "";

    [Description("Purpose: shop, quest_giver, crafting, medical, barracks, tavern, storage, or other")]
    public string Purpose { get; set; } = "";

    [Description("Visual description for 3D asset generation (exterior only)")]
    public string VisualDescription { get; set; } = "";

    [Description("Size category: small, medium, or large")]
    public string SizeCategory { get; set; } = "medium";

    [Description("One sentence: what this building was in reality — real name, style, era, purpose")]
    public string OriginalDescription { get; set; } = "";

    [Description("30–60 word Meshy text-to-3D prompt: materials, damage, style. End with 'low-poly game asset'")]
    public string MeshyPrompt { get; set; } = "";

    [Description("Compass direction + nearby feature relative to town centre (e.g. 'south along main road')")]
    public string PositionHint { get; set; } = "";
}

internal sealed class LlmHazardEntry
{
    [Description("Hazard type: flooding, radiation, collapse, fire, toxic, wildlife, or other")]
    public string Type { get; set; } = "";

    [Description("Description of the hazard")]
    public string Description { get; set; } = "";

    [Description("Where in the town the hazard is located (e.g. 'south-west waterfront')")]
    public string LocationHint { get; set; } = "";
}
