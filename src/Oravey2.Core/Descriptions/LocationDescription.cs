namespace Oravey2.Core.Descriptions;

/// <summary>
/// Type of location for description generation.
/// </summary>
public enum LocationType : byte
{
    Poi,
    Town,
    Region,
    Area,
    Building,
    Dungeon
}

/// <summary>
/// Three-tier description for a location: tagline (always present),
/// summary (generated on first open), dossier (generated on "Read more").
/// </summary>
public sealed class LocationDescription
{
    public int LocationId { get; set; }
    public LocationType Type { get; set; }
    public string Tagline { get; set; } = "";
    public string? Summary { get; set; }
    public string? Dossier { get; set; }
    public DateTime? SummaryUtc { get; set; }
    public DateTime? DossierUtc { get; set; }
    public string? LlmModel { get; set; }
}
