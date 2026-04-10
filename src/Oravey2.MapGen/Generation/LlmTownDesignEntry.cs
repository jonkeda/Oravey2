using System.ComponentModel;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Schema class for the LLM town-design tool call.
/// Properties map to the JSON schema that AIFunctionFactory exposes to the model.
/// </summary>
internal sealed class LlmTownDesignEntry
{
    [Description("Name of the single landmark building")]
    public string LandmarkName { get; set; } = "";

    [Description("Visual description of the landmark for 3D asset generation (e.g. 'A massive coastal fortress with crumbling stone walls')")]
    public string LandmarkVisualDescription { get; set; } = "";

    [Description("Size category of the landmark: small, medium, or large")]
    public string LandmarkSizeCategory { get; set; } = "large";

    [Description("Array of key locations in the town")]
    public List<LlmKeyLocationEntry> KeyLocations { get; set; } = [];

    [Description("Layout style: grid, radial, organic, linear, clustered, or compound")]
    public string LayoutStyle { get; set; } = "organic";

    [Description("Array of environmental hazards (0 to 3)")]
    public List<LlmHazardEntry> Hazards { get; set; } = [];
}

internal sealed class LlmKeyLocationEntry
{
    [Description("Name of the location")]
    public string Name { get; set; } = "";

    [Description("Purpose: shop, quest_giver, crafting, medical, barracks, tavern, storage, or other")]
    public string Purpose { get; set; } = "";

    [Description("Visual description for 3D asset generation")]
    public string VisualDescription { get; set; } = "";

    [Description("Size category: small, medium, or large")]
    public string SizeCategory { get; set; } = "medium";
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
