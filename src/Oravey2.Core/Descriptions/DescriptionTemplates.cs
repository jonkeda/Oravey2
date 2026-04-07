using Oravey2.Core.World;

namespace Oravey2.Core.Descriptions;

/// <summary>
/// Template-based description generator. Produces taglines and summaries
/// from POI type × biome combinations without requiring an LLM.
/// </summary>
public static class DescriptionTemplates
{
    /// <summary>
    /// Returns a tagline (≤80 chars) for a location based on its type and biome.
    /// </summary>
    public static string GetTagline(string poiType, BiomeType biome, string locationName)
    {
        var template = GetTaglineTemplate(poiType, biome);
        return template.Replace("{name}", locationName);
    }

    /// <summary>
    /// Returns a template-expanded summary paragraph for template-only locations.
    /// </summary>
    public static string GetSummary(string poiType, BiomeType biome, string locationName)
    {
        var template = GetSummaryTemplate(poiType, biome);
        return template.Replace("{name}", locationName);
    }

    /// <summary>
    /// Returns a fallback tagline template for a given POI type × biome combination.
    /// </summary>
    internal static string GetTaglineTemplate(string poiType, BiomeType biome)
    {
        var key = (NormalizeType(poiType), biome);
        return TaglineTemplates.GetValueOrDefault(key)
            ?? TypeOnlyTaglines.GetValueOrDefault(NormalizeType(poiType))
            ?? FallbackTagline;
    }

    /// <summary>
    /// Returns a fallback summary template for a given POI type × biome combination.
    /// </summary>
    internal static string GetSummaryTemplate(string poiType, BiomeType biome)
    {
        var key = (NormalizeType(poiType), biome);
        return SummaryTemplates.GetValueOrDefault(key)
            ?? TypeOnlySummaries.GetValueOrDefault(NormalizeType(poiType))
            ?? FallbackSummary;
    }

    private static string NormalizeType(string poiType) =>
        poiType.Trim().ToLowerInvariant();

    private const string FallbackTagline = "A forgotten place in the wasteland.";
    private const string FallbackSummary = "{name} stands as a silent testament to the old world. Little is known about what remains here, but scavengers may find something of value among the ruins.";

    private static readonly Dictionary<(string Type, BiomeType Biome), string> TaglineTemplates = new()
    {
        [("town", BiomeType.Wasteland)] = "The ruins of {name} bake under a relentless sun.",
        [("town", BiomeType.RuinedCity)] = "Crumbling towers mark the remains of {name}.",
        [("town", BiomeType.Settlement)] = "{name} — a fortified community clinging to survival.",
        [("town", BiomeType.ForestOvergrown)] = "{name} hides beneath a canopy of mutated growth.",
        [("town", BiomeType.Coastal)] = "Salt-crusted walls of {name} face the toxic tide.",
        [("town", BiomeType.Industrial)] = "Smokestacks of {name} still stand, rusted and hollow.",
        [("town", BiomeType.IrradiatedCrater)] = "The glowing remnants of {name} pulse with danger.",
        [("town", BiomeType.Bunker)] = "Deep beneath the surface, {name} endures.",
        [("town", BiomeType.Underground)] = "{name} sprawls through forgotten tunnels and vaults.",

        [("gas_station", BiomeType.Wasteland)] = "A sun-bleached fuel stop on a cracked highway.",
        [("gas_station", BiomeType.RuinedCity)] = "An urban fuel depot, long since drained.",
        [("checkpoint", BiomeType.Wasteland)] = "A makeshift barricade guards this dusty crossing.",
        [("checkpoint", BiomeType.Settlement)] = "A fortified gate controls access to the community.",
        [("ruin", BiomeType.Wasteland)] = "Scattered debris marks what once stood here.",
        [("ruin", BiomeType.RuinedCity)] = "Collapsed floors and twisted rebar fill this city block.",
        [("bunker", BiomeType.Bunker)] = "A sealed entrance leads into the earth.",
        [("bunker", BiomeType.Wasteland)] = "A half-buried hatch promises safety — or a trap.",
        [("camp", BiomeType.Wasteland)] = "A circle of tents and a dying fire in the dust.",
        [("camp", BiomeType.ForestOvergrown)] = "A hidden camp nestled among the twisted trees.",
    };

    private static readonly Dictionary<string, string> TypeOnlyTaglines = new()
    {
        ["town"] = "{name} — a settlement in the wasteland.",
        ["gas_station"] = "An abandoned fuel station.",
        ["checkpoint"] = "A roadside checkpoint.",
        ["ruin"] = "Rubble and memories.",
        ["bunker"] = "A sealed underground shelter.",
        ["camp"] = "A makeshift survivor camp.",
        ["bridge"] = "A crumbling overpass.",
        ["warehouse"] = "A cavernous storage facility.",
        ["hospital"] = "A ransacked medical centre.",
        ["school"] = "An overgrown schoolyard.",
    };

    private static readonly Dictionary<(string Type, BiomeType Biome), string> SummaryTemplates = new()
    {
        [("town", BiomeType.Wasteland)] = "{name} was once a thriving community before the bombs fell. Now its streets are choked with dust and debris. A few hardy survivors have fortified the central block, trading scavenged goods and guarding a precious well. Approach with caution — strangers aren't always welcome.",
        [("town", BiomeType.RuinedCity)] = "{name} occupies several blocks of a ruined metropolis. The upper floors have collapsed, but the ground level has been cleared and reinforced. Market stalls line the main corridor, and the sound of hammering echoes through the concrete canyons. A militia patrols the perimeter.",
        [("town", BiomeType.Settlement)] = "{name} is a walled enclave with functioning agriculture and basic trade. The community council enforces strict rules about water usage and weapon storage. New arrivals are quarantined for three days before being allowed past the inner gate.",
        [("town", BiomeType.ForestOvergrown)] = "{name} has been reclaimed by the forest. Homes are built into massive tree trunks and connected by rope bridges. The canopy provides natural camouflage, making the settlement nearly invisible from above. Residents trade in medicinal herbs and wild game.",
        [("town", BiomeType.Coastal)] = "{name} clings to the shoreline where the old harbour once thrived. Fishing boats, patched and re-patched, provide the main food source. The salt air carries the constant threat of toxic storms rolling in from the poisoned ocean.",
        [("gas_station", BiomeType.Wasteland)] = "This fuel station sits at a crossroads, its pumps long dry. The attached diner has been converted into a way-station where travellers exchange news and barter. Fuel cans occasionally surface, commanding a king's ransom.",
        [("ruin", BiomeType.RuinedCity)] = "Little remains of this city block except rubble and rusted frames. Scavengers have picked over the surface, but deeper layers may still hold pre-war supplies for those brave enough to dig.",
    };

    private static readonly Dictionary<string, string> TypeOnlySummaries = new()
    {
        ["town"] = "{name} is a settlement that has survived the apocalypse through determination and resourcefulness. Its inhabitants trade with passing travellers and defend their walls against raiders and worse. The atmosphere is tense but hopeful.",
        ["gas_station"] = "An abandoned fuel station stripped of most valuables. The underground tanks may still hold trace amounts of fuel, and the shop shelves occasionally yield overlooked supplies. A useful rest stop for weary travellers.",
        ["checkpoint"] = "A roadside checkpoint, either abandoned or manned by a local faction. Barriers made from old vehicles block the path. Travellers must either negotiate passage or find an alternate route.",
        ["ruin"] = "The remains of a pre-war structure, collapsed and overgrown. Scavengers have taken the obvious loot, but patient searchers may find hidden caches in the rubble.",
        ["bunker"] = "An underground shelter from the old world. The entrance is partially concealed. Inside, the air is stale but the structure seems intact. What's sealed behind the blast door is anyone's guess.",
        ["camp"] = "A makeshift camp set up by survivors. Tents or lean-tos surround a fire pit. The occupants may be friendly traders, desperate refugees, or something more dangerous.",
    };
}
