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
public sealed record LocationDescription(
    int LocationId,
    LocationType Type,
    string Tagline,
    string? Summary = null,
    string? Dossier = null,
    DateTime? SummaryUtc = null,
    DateTime? DossierUtc = null,
    string? LlmModel = null);
