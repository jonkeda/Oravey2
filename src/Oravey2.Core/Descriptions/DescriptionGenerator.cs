using Oravey2.Core.World;

namespace Oravey2.Core.Descriptions;

/// <summary>
/// Context data passed to the LLM prompt builder for location descriptions.
/// </summary>
public sealed record LocationContext(
    string Name,
    string PoiType,
    BiomeType Biome,
    string? RegionName = null,
    string? ExistingTagline = null,
    string? ExistingSummary = null);

/// <summary>
/// Generates location descriptions using an LLM with template-based fallback.
/// The LLM delegate follows the same pattern as TownCurator: Func&lt;string, CancellationToken, Task&lt;string&gt;&gt;.
/// </summary>
public sealed class DescriptionGenerator
{
    private readonly Func<string, CancellationToken, Task<string>>? _llmCall;
    private const int MaxRetries = 2;

    public DescriptionGenerator(Func<string, CancellationToken, Task<string>>? llmCall = null)
    {
        _llmCall = llmCall;
    }

    /// <summary>
    /// Generates a summary (Tier 2) for a location.
    /// Uses LLM if available, falls back to template.
    /// </summary>
    public async Task<string> GenerateSummaryAsync(LocationContext context, CancellationToken ct = default)
    {
        if (_llmCall != null)
        {
            var prompt = BuildSummaryPrompt(context);
            var result = await CallLlmWithRetry(prompt, ct);
            if (result != null)
                return TruncateToLength(result, 300);
        }

        return DescriptionTemplates.GetSummary(context.PoiType, context.Biome, context.Name);
    }

    /// <summary>
    /// Generates a dossier (Tier 3) for a location.
    /// Uses LLM if available, falls back to a combined template summary + generic dossier text.
    /// </summary>
    public async Task<string> GenerateDossierAsync(LocationContext context, CancellationToken ct = default)
    {
        if (_llmCall != null)
        {
            var prompt = BuildDossierPrompt(context);
            var result = await CallLlmWithRetry(prompt, ct);
            if (result != null)
                return TruncateToLength(result, 1500);
        }

        // Fallback: template summary + generic dossier padding
        var summary = DescriptionTemplates.GetSummary(context.PoiType, context.Biome, context.Name);
        return $"{summary}\n\nDetailed report requires communications link.";
    }

    private async Task<string?> CallLlmWithRetry(string prompt, CancellationToken ct)
    {
        for (int attempt = 0; attempt <= MaxRetries; attempt++)
        {
            try
            {
                var response = await _llmCall!(prompt, ct);
                if (!string.IsNullOrWhiteSpace(response))
                    return response.Trim();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                // Retry on failure
                if (attempt == MaxRetries)
                    return null;
            }
        }
        return null;
    }

    internal static string BuildSummaryPrompt(LocationContext context)
    {
        var parts = new List<string>
        {
            "You are a post-apocalyptic world writer. Write a concise summary paragraph (max 300 characters) for a location.",
            $"Location name: {context.Name}",
            $"Type: {context.PoiType}",
            $"Biome: {context.Biome}",
        };
        if (context.RegionName != null) parts.Add($"Region: {context.RegionName}");
        if (context.ExistingTagline != null) parts.Add($"Tagline: {context.ExistingTagline}");
        parts.Add("Write in a grim, atmospheric tone. Stay in-world. No meta-commentary.");
        return string.Join("\n", parts);
    }

    internal static string BuildDossierPrompt(LocationContext context)
    {
        var parts = new List<string>
        {
            "You are a post-apocalyptic world writer. Write a detailed multi-paragraph dossier (max 1500 characters) for a location.",
            $"Location name: {context.Name}",
            $"Type: {context.PoiType}",
            $"Biome: {context.Biome}",
        };
        if (context.RegionName != null) parts.Add($"Region: {context.RegionName}");
        if (context.ExistingTagline != null) parts.Add($"Tagline: {context.ExistingTagline}");
        if (context.ExistingSummary != null) parts.Add($"Summary: {context.ExistingSummary}");
        parts.Add("Include: sensory details, rumours, loot hints, faction presence, history. Stay in-world.");
        return string.Join("\n", parts);
    }

    private static string TruncateToLength(string text, int maxLength) =>
        text.Length <= maxLength ? text : text[..maxLength];
}
