using System.Reflection;
using System.Text.Json;

namespace Oravey2.MapGen.Assets;

public sealed class AssetRegistry : IAssetRegistry
{
    private readonly Dictionary<string, List<AssetEntry>> _catalog;

    public AssetRegistry()
    {
        _catalog = LoadEmbeddedCatalog();
    }

    public AssetRegistry(Dictionary<string, List<AssetEntry>> catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyList<AssetEntry> Search(string assetType, string query)
    {
        if (!_catalog.TryGetValue(assetType, out var entries))
            return Array.Empty<AssetEntry>();

        if (string.IsNullOrWhiteSpace(query))
            return entries;

        return entries
            .Where(e =>
                e.Id.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Description.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                e.Tags.Any(t => t.Contains(query, StringComparison.OrdinalIgnoreCase)))
            .ToList();
    }

    public IReadOnlyList<AssetEntry> ListPrefabs(string category)
    {
        if (!_catalog.TryGetValue(category, out var entries))
            return Array.Empty<AssetEntry>();

        return entries;
    }

    public bool Exists(string assetType, string id)
    {
        if (!_catalog.TryGetValue(assetType, out var entries))
            return false;

        return entries.Any(e => e.Id.Equals(id, StringComparison.OrdinalIgnoreCase));
    }

    private static Dictionary<string, List<AssetEntry>> LoadEmbeddedCatalog()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var resourceName = assembly.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith("asset-catalog.json", StringComparison.OrdinalIgnoreCase));

        if (resourceName is null)
            throw new InvalidOperationException("Embedded resource 'asset-catalog.json' not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)!;
        var doc = JsonDocument.Parse(stream);
        var catalog = new Dictionary<string, List<AssetEntry>>();

        foreach (var category in doc.RootElement.EnumerateObject())
        {
            var entries = new List<AssetEntry>();
            foreach (var item in category.Value.EnumerateArray())
            {
                var id = item.GetProperty("id").GetString()!;
                var description = item.GetProperty("description").GetString()!;
                var tags = item.GetProperty("tags").EnumerateArray()
                    .Select(t => t.GetString()!)
                    .ToArray();
                entries.Add(new AssetEntry(id, description, tags));
            }
            catalog[category.Name] = entries;
        }

        return catalog;
    }
}
