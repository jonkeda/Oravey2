using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.Core.Content;

/// <summary>
/// Scenario definition loaded from a content pack's scenarios/ directory.
/// </summary>
public sealed record ScenarioDefinition(
    string Id,
    string Name,
    string Description,
    string? Map = null,
    string[]? Features = null,
    int Difficulty = 1,
    string[]? Tags = null);

/// <summary>
/// Info about an installed content pack (manifest + directory).
/// </summary>
public sealed record ContentPackInfo(
    string Directory,
    ContentManifest Manifest);

/// <summary>
/// Discovers and manages installed content packs from ContentPacks/.
/// </summary>
public sealed class ContentPackService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        ReadCommentHandling = JsonCommentHandling.Skip
    };

    private ContentPackInfo[] _packs = [];
    private ContentPackInfo? _activePack;

    /// <summary>All discovered content packs.</summary>
    public IReadOnlyList<ContentPackInfo> Packs => _packs;

    /// <summary>The currently active content pack, or null.</summary>
    public ContentPackInfo? ActivePack => _activePack;

    /// <summary>
    /// Scans ContentPacks/ directory for installed packs.
    /// </summary>
    public void DiscoverPacks()
    {
        var contentPacksDir = Path.Combine(AppContext.BaseDirectory, "ContentPacks");
        if (!Directory.Exists(contentPacksDir))
        {
            _packs = [];
            return;
        }

        var packs = new List<ContentPackInfo>();
        foreach (var dir in System.IO.Directory.GetDirectories(contentPacksDir))
        {
            var manifestPath = Path.Combine(dir, "manifest.json");
            if (!File.Exists(manifestPath)) continue;

            try
            {
                var json = File.ReadAllText(manifestPath);
                var manifest = JsonSerializer.Deserialize<ContentManifest>(json, JsonOptions);
                if (manifest != null)
                    packs.Add(new ContentPackInfo(dir, manifest));
            }
            catch
            {
                // Skip malformed manifests
            }
        }

        _packs = packs.ToArray();
    }

    /// <summary>
    /// Sets the active content pack by manifest ID (e.g. "oravey2.apocalyptic").
    /// Returns true if found and activated.
    /// </summary>
    public bool SetActivePack(string packId)
    {
        var pack = _packs.FirstOrDefault(p =>
            p.Manifest.Id.Equals(packId, StringComparison.OrdinalIgnoreCase));

        _activePack = pack;
        return pack != null;
    }

    /// <summary>
    /// Loads scenario definitions from the active content pack's scenarios/ directory.
    /// </summary>
    public ScenarioDefinition[] LoadScenarios()
    {
        if (_activePack == null) return [];

        var scenariosDir = Path.Combine(_activePack.Directory, "scenarios");
        if (!System.IO.Directory.Exists(scenariosDir)) return [];

        var scenarios = new List<ScenarioDefinition>();
        foreach (var file in System.IO.Directory.GetFiles(scenariosDir, "*.json"))
        {
            try
            {
                var json = File.ReadAllText(file);
                var scenario = JsonSerializer.Deserialize<ScenarioDefinition>(json, JsonOptions);
                if (scenario != null)
                    scenarios.Add(scenario);
            }
            catch
            {
                // Skip malformed scenario files
            }
        }

        return scenarios.ToArray();
    }

    /// <summary>
    /// Creates a ContentPackLoader for the active pack.
    /// Returns null if no pack is active.
    /// </summary>
    public ContentPackLoader? CreateLoader()
    {
        return _activePack != null ? new ContentPackLoader(_activePack.Directory) : null;
    }

    /// <summary>
    /// Gets the catalog.json path from the active pack.
    /// Returns null if no pack is active or catalog doesn't exist.
    /// </summary>
    public string? GetCatalogPath()
    {
        if (_activePack == null) return null;
        var path = Path.Combine(_activePack.Directory, "catalog.json");
        return File.Exists(path) ? path : null;
    }
}
