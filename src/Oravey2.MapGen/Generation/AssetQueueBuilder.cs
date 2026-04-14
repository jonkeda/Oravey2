namespace Oravey2.MapGen.Generation;

/// <summary>
/// Scans design.json files in a content pack and builds a deduplicated list
/// of 3D asset requests for Meshy generation.
/// </summary>
public sealed class AssetQueueBuilder
{
    /// <summary>
    /// Builds the asset queue by scanning all design.json files under the
    /// content pack's towns/ directory. Deduplicates by VisualDescription.
    /// Marks assets as Ready if their .glb already exists on disk.
    /// </summary>
    public List<AssetRequest> BuildQueue(string contentPackPath)
    {
        var townsDir = Path.Combine(contentPackPath, "towns");
        if (!Directory.Exists(townsDir))
            return [];

        var seen = new Dictionary<string, AssetRequest>(StringComparer.OrdinalIgnoreCase);
        var queue = new List<AssetRequest>();

        foreach (var townDir in Directory.GetDirectories(townsDir))
        {
            var designPath = Path.Combine(townDir, "design.json");
            if (!File.Exists(designPath)) continue;

            var design = TownDesignFile.Load(designPath).ToTownDesign();
            var townName = design.TownName;

            // Landmarks
            foreach (var lm in design.Landmarks)
            {
                AddAsset(seen, queue, townName, lm.Name,
                    lm.VisualDescription, lm.SizeCategory,
                    contentPackPath);
            }

            // Key locations
            foreach (var loc in design.KeyLocations)
            {
                AddAsset(seen, queue, townName, loc.Name,
                    loc.VisualDescription, loc.SizeCategory,
                    contentPackPath);
            }
        }

        return queue;
    }

    private static void AddAsset(
        Dictionary<string, AssetRequest> seen,
        List<AssetRequest> queue,
        string townName,
        string locationName,
        string visualDescription,
        string sizeCategory,
        string contentPackPath)
    {
        if (string.IsNullOrWhiteSpace(visualDescription)) return;

        // Deduplicate by visual description
        if (seen.ContainsKey(visualDescription))
            return;

        var assetId = DeriveAssetId(townName, locationName);
        var glbPath = Path.Combine(contentPackPath, "assets", "meshes", $"{assetId}.glb");
        var status = File.Exists(glbPath) ? AssetStatus.Ready : AssetStatus.Pending;

        var request = new AssetRequest
        {
            AssetId = assetId, TownName = townName, LocationName = locationName,
            VisualDescription = visualDescription, SizeCategory = sizeCategory, Status = status,
        };
        seen[visualDescription] = request;
        queue.Add(request);
    }

    internal static string DeriveAssetId(string townName, string locationName)
    {
        var combined = $"{townName}-{locationName}";
        return combined
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');
    }
}
