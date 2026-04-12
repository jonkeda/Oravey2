using Oravey2.MapGen.Assets;

namespace Oravey2.MapGen.Generation;

/// <summary>
/// Scans all towns in a content pack and returns merged <see cref="TownAssetSummary"/>
/// records that combine design.json (step 07) with buildings.json/props.json (step 08).
/// </summary>
public sealed class TownAssetScanner
{
    /// <summary>
    /// Scans all towns in the content pack and returns merged summaries.
    /// </summary>
    public List<TownAssetSummary> Scan(string contentPackPath)
    {
        var townsDir = Path.Combine(contentPackPath, "towns");
        if (!Directory.Exists(townsDir))
            return [];

        var results = new List<TownAssetSummary>();

        foreach (var townDir in Directory.GetDirectories(townsDir))
        {
            var designPath = Path.Combine(townDir, "design.json");
            if (!File.Exists(designPath)) continue;

            var design = TownDesignFile.Load(designPath).ToTownDesign();
            var gameName = Path.GetFileName(townDir);

            var buildingsPath = Path.Combine(townDir, "buildings.json");
            var propsPath = Path.Combine(townDir, "props.json");

            List<PlacedBuilding> placedBuildings = [];
            List<PlacedProp> placedProps = [];

            if (File.Exists(buildingsPath) && File.Exists(Path.Combine(townDir, "layout.json")))
            {
                var mapResult = TownMapFiles.Load(townDir);
                placedBuildings = mapResult.Buildings;
                placedProps = mapResult.Props;
            }

            var buildings = MergeBuildings(design, placedBuildings, contentPackPath);
            var props = MergeProps(placedProps, contentPackPath);

            results.Add(new TownAssetSummary(
                design.TownName, gameName, design.LayoutStyle, buildings, props));
        }

        return results;
    }

    internal static List<BuildingAssetEntry> MergeBuildings(
        TownDesign design, List<PlacedBuilding> placed, string contentPackPath)
    {
        var entries = new List<BuildingAssetEntry>();

        // Build a lookup of placed buildings by name (case-insensitive)
        var placedByName = new Dictionary<string, PlacedBuilding>(StringComparer.OrdinalIgnoreCase);
        foreach (var b in placed)
        {
            placedByName.TryAdd(b.Name, b);
        }

        // Landmarks
        foreach (var lm in design.Landmarks)
        {
            AddBuildingEntry(entries, lm.Name, "landmark",
                lm.SizeCategory, lm.VisualDescription,
                placedByName, contentPackPath);
        }

        // Key locations
        foreach (var loc in design.KeyLocations)
        {
            AddBuildingEntry(entries, loc.Name, "key",
                loc.SizeCategory, loc.VisualDescription,
                placedByName, contentPackPath);
        }

        // Any placed buildings not in the design (generic fill buildings)
        foreach (var b in placed)
        {
            if (entries.Any(e => string.Equals(e.Name, b.Name, StringComparison.OrdinalIgnoreCase)))
                continue;

            var meshStatus = ClassifyMeshStatus(b.MeshAsset, contentPackPath);
            entries.Add(new BuildingAssetEntry(
                b.Id, b.Name, "generic", b.SizeCategory, "", b.MeshAsset,
                meshStatus, b.Floors, b.Condition));
        }

        return entries;
    }

    private static void AddBuildingEntry(
        List<BuildingAssetEntry> entries,
        string name, string role, string sizeCategory, string visualDescription,
        Dictionary<string, PlacedBuilding> placedByName, string contentPackPath)
    {
        if (placedByName.TryGetValue(name, out var placed))
        {
            var meshStatus = ClassifyMeshStatus(placed.MeshAsset, contentPackPath);
            entries.Add(new BuildingAssetEntry(
                placed.Id, name, role, sizeCategory, visualDescription,
                placed.MeshAsset, meshStatus, placed.Floors, placed.Condition));
        }
        else
        {
            // Design entry without placement data yet
            entries.Add(new BuildingAssetEntry(
                "", name, role, sizeCategory, visualDescription,
                "", MeshStatus.None, 0, 0));
        }
    }

    internal static List<PropAssetEntry> MergeProps(
        List<PlacedProp> placed, string contentPackPath)
    {
        return placed.Select(p =>
            new PropAssetEntry(p.Id, p.MeshAsset,
                ClassifyMeshStatus(p.MeshAsset, contentPackPath))).ToList();
    }

    /// <summary>
    /// Classifies the mesh status of a path.
    /// </summary>
    internal static MeshStatus ClassifyMeshStatus(string meshAsset, string contentPackPath)
    {
        if (string.IsNullOrWhiteSpace(meshAsset))
            return MeshStatus.None;

        if (meshAsset.Contains("primitives/", StringComparison.OrdinalIgnoreCase))
            return MeshStatus.Primitive;

        // Check if the file exists on disk
        var fullPath = Path.Combine(contentPackPath, "assets", meshAsset);
        if (File.Exists(fullPath))
            return MeshStatus.Ready;

        return MeshStatus.None;
    }

    /// <summary>
    /// Derives the Meshy asset ID from town name + location name (lowercase kebab-case).
    /// </summary>
    internal static string DeriveAssetId(string townName, string locationName)
    {
        var combined = $"{townName}-{locationName}";
        return combined
            .ToLowerInvariant()
            .Replace(' ', '-')
            .Replace('_', '-');
    }
}
