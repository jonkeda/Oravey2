using Oravey2.Core.Data;
using Oravey2.Core.World;

namespace Oravey2.Core.Descriptions;

/// <summary>
/// Orchestrates location description retrieval with caching.
/// Checks SQLite first; on cache miss, generates via LLM or template and stores the result.
/// </summary>
public sealed class DescriptionService
{
    private readonly WorldMapStore _store;
    private readonly DescriptionGenerator _generator;

    public DescriptionService(WorldMapStore store, DescriptionGenerator generator)
    {
        _store = store;
        _generator = generator;
    }

    /// <summary>
    /// Returns the tagline for a location (always synchronous — taglines are pre-generated).
    /// Returns null if no description exists for the location.
    /// </summary>
    public string? GetTagline(int locationId)
    {
        return _store.GetDescription(locationId)?.Tagline;
    }

    /// <summary>
    /// Returns the full cached description, or null if the location has no description entry.
    /// </summary>
    public LocationDescription? GetCached(int locationId)
    {
        return _store.GetDescription(locationId);
    }

    /// <summary>
    /// Returns the summary (Tier 2), generating it if not yet cached.
    /// </summary>
    public async Task<string> GetOrGenerateSummaryAsync(
        int locationId, LocationContext context, CancellationToken ct = default)
    {
        var existing = _store.GetDescription(locationId);
        if (existing?.Summary is not null)
            return existing.Summary;

        var summary = await _generator.GenerateSummaryAsync(context, ct);

        if (existing != null)
            _store.UpdateDescriptionSummary(locationId, summary);
        else
            _store.InsertDescription(locationId, context.PoiType, context.ExistingTagline ?? context.Name, summary);

        return summary;
    }

    /// <summary>
    /// Returns the dossier (Tier 3), generating it if not yet cached.
    /// </summary>
    public async Task<string> GetOrGenerateDossierAsync(
        int locationId, LocationContext context, CancellationToken ct = default)
    {
        var existing = _store.GetDescription(locationId);
        if (existing?.Dossier is not null)
            return existing.Dossier;

        var dossier = await _generator.GenerateDossierAsync(context, ct);

        if (existing != null)
            _store.UpdateDescriptionDossier(locationId, dossier);
        else
            _store.InsertDescription(locationId, context.PoiType, context.ExistingTagline ?? context.Name, dossier: dossier);

        return dossier;
    }

    /// <summary>
    /// Invalidates a cached description so the next request triggers re-generation.
    /// </summary>
    public void Invalidate(int locationId)
    {
        _store.DeleteDescription(locationId);
    }
}
