using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Pipeline;

public sealed class ContentPackAssembler
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>
    /// Generate or update the scenario file linking all towns.
    /// </summary>
    public void GenerateScenario(string contentPackPath, List<string> townGameNames, ScenarioSettings settings)
    {
        var scenariosDir = Path.Combine(contentPackPath, "scenarios");
        Directory.CreateDirectory(scenariosDir);

        var scenario = new ScenarioFile
        {
            Id = settings.Id,
            Name = settings.Name,
            Description = settings.Description,
            Towns = townGameNames,
            PlayerStart = settings.PlayerStart,
            Difficulty = settings.Difficulty,
            Tags = settings.Tags,
        };

        var path = Path.Combine(scenariosDir, $"{settings.Id}.json");
        File.WriteAllText(path, JsonSerializer.Serialize(scenario, JsonOptions));
    }

    /// <summary>
    /// Rebuild catalog.json from all assets in the pack.
    /// </summary>
    public void RebuildCatalog(string contentPackPath)
    {
        var meshesDir = Path.Combine(contentPackPath, "assets", "meshes");
        var catalog = new CatalogFile();

        if (Directory.Exists(meshesDir))
        {
            foreach (var glb in Directory.EnumerateFiles(meshesDir, "*.glb", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(contentPackPath, glb)
                    .Replace('\\', '/');
                var metaPath = Path.ChangeExtension(glb, ".meta.json");
                var category = "building";
                if (File.Exists(metaPath))
                {
                    try
                    {
                        var meta = JsonSerializer.Deserialize<MetaCategoryDto>(
                            File.ReadAllText(metaPath), JsonOptions);
                        if (meta is not null && !string.IsNullOrEmpty(meta.SourceType))
                            category = meta.SourceType == "prop" ? "prop" : "building";
                    }
                    catch { /* skip malformed meta */ }
                }

                catalog.Building.Add(new CatalogEntry { AssetId = Path.GetFileNameWithoutExtension(glb), Path = relativePath });
            }
        }

        var catalogPath = Path.Combine(contentPackPath, "catalog.json");
        File.WriteAllText(catalogPath, JsonSerializer.Serialize(catalog, JsonOptions));
    }

    /// <summary>
    /// Update manifest.json with current version/metadata.
    /// </summary>
    public void UpdateManifest(string contentPackPath, ManifestUpdate update)
    {
        var manifestPath = Path.Combine(contentPackPath, "manifest.json");
        ManifestFile manifest;

        if (File.Exists(manifestPath))
        {
            manifest = JsonSerializer.Deserialize<ManifestFile>(
                File.ReadAllText(manifestPath), JsonOptions) ?? new();
        }
        else
        {
            manifest = new ManifestFile();
        }

        if (!string.IsNullOrEmpty(update.Name))
            manifest.Name = update.Name;
        if (!string.IsNullOrEmpty(update.Version))
            manifest.Version = update.Version;
        if (!string.IsNullOrEmpty(update.Description))
            manifest.Description = update.Description;
        if (!string.IsNullOrEmpty(update.Author))
            manifest.Author = update.Author;

        Directory.CreateDirectory(contentPackPath);
        File.WriteAllText(manifestPath, JsonSerializer.Serialize(manifest, JsonOptions));
    }

    /// <summary>
    /// Validate all cross-references in the content pack.
    /// </summary>
    public ValidationResult Validate(string contentPackPath)
    {
        var result = new ValidationResult();

        ValidateManifest(contentPackPath, result);
        ValidateCuratedTowns(contentPackPath, result);
        ValidateTownFiles(contentPackPath, result);
        ValidateMeshReferences(contentPackPath, result);
        ValidateOrphanMeshes(contentPackPath, result);
        ValidateCatalog(contentPackPath, result);
        ValidateScenarios(contentPackPath, result);

        return result;
    }

    private static void ValidateManifest(string contentPackPath, ValidationResult result)
    {
        var manifestPath = Path.Combine(contentPackPath, "manifest.json");
        if (!File.Exists(manifestPath))
        {
            result.AddError("Manifest", "manifest.json does not exist.");
            return;
        }

        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(manifestPath));
            var hasParent = doc.RootElement.TryGetProperty("parent", out _);
            result.AddPass("Manifest", "manifest.json exists and is valid JSON.");
            if (!hasParent)
                result.AddWarning("Manifest", "No 'parent' field in manifest.json.");
        }
        catch (JsonException ex)
        {
            result.AddError("Manifest", $"manifest.json is not valid JSON: {ex.Message}");
        }
    }

    private static void ValidateCuratedTowns(string contentPackPath, ValidationResult result)
    {
        var curatedPath = Path.Combine(contentPackPath, "data", "curated-towns.json");
        if (!File.Exists(curatedPath))
        {
            result.AddWarning("Curated Towns", "data/curated-towns.json not found.");
            return;
        }

        try
        {
            var doc = JsonDocument.Parse(File.ReadAllText(curatedPath));
            if (doc.RootElement.TryGetProperty("towns", out var towns) && towns.ValueKind == JsonValueKind.Array)
            {
                var townNames = towns.EnumerateArray()
                    .Select(t => t.TryGetProperty("gameName", out var gn) ? gn.GetString() : null)
                    .Where(n => n is not null)
                    .ToList();

                var townsDir = Path.Combine(contentPackPath, "towns");
                foreach (var name in townNames)
                {
                    var townDir = Path.Combine(townsDir, name!);
                    var designPath = Path.Combine(townDir, "design.json");
                    if (!File.Exists(designPath))
                        result.AddError("Town Design", $"Town '{name}' in curated-towns.json has no design.json.");
                }

                result.AddPass("Curated Towns", $"{townNames.Count} towns listed in curated-towns.json.");
            }
        }
        catch (JsonException ex)
        {
            result.AddError("Curated Towns", $"curated-towns.json parse error: {ex.Message}");
        }
    }

    private static void ValidateTownFiles(string contentPackPath, ValidationResult result)
    {
        var townsDir = Path.Combine(contentPackPath, "towns");
        if (!Directory.Exists(townsDir))
        {
            result.AddWarning("Town Files", "towns/ directory does not exist.");
            return;
        }

        var requiredFiles = new[] { "layout.json", "buildings.json", "props.json", "zones.json" };
        var townDirs = Directory.GetDirectories(townsDir);
        var checkedCount = 0;

        foreach (var townDir in townDirs)
        {
            var townName = Path.GetFileName(townDir);
            if (!File.Exists(Path.Combine(townDir, "design.json")))
                continue; // Skip placeholder directories

            checkedCount++;
            foreach (var required in requiredFiles)
            {
                if (!File.Exists(Path.Combine(townDir, required)))
                    result.AddError("Town Maps", $"Town '{townName}' is missing {required}.");
            }
        }

        if (checkedCount > 0)
            result.AddPass("Town Files", $"{checkedCount} designed towns checked for map files.");
    }

    private static void ValidateMeshReferences(string contentPackPath, ValidationResult result)
    {
        var townsDir = Path.Combine(contentPackPath, "towns");
        if (!Directory.Exists(townsDir)) return;

        var brokenCount = 0;
        var checkedCount = 0;

        foreach (var townDir in Directory.GetDirectories(townsDir))
        {
            var buildingsPath = Path.Combine(townDir, "buildings.json");
            if (!File.Exists(buildingsPath)) continue;

            try
            {
                var buildings = JsonSerializer.Deserialize<List<MeshRefDto>>(
                    File.ReadAllText(buildingsPath), JsonOptions) ?? [];

                foreach (var b in buildings)
                {
                    if (string.IsNullOrEmpty(b.MeshAsset)) continue;
                    checkedCount++;
                    var meshPath = Path.Combine(contentPackPath, b.MeshAsset.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(meshPath))
                    {
                        brokenCount++;
                        result.AddError("Mesh Refs", $"Building '{b.Name}' references non-existent mesh: {b.MeshAsset}");
                    }
                }
            }
            catch { /* skip malformed */ }

            var propsPath = Path.Combine(townDir, "props.json");
            if (!File.Exists(propsPath)) continue;

            try
            {
                var props = JsonSerializer.Deserialize<List<MeshRefDto>>(
                    File.ReadAllText(propsPath), JsonOptions) ?? [];

                foreach (var p in props)
                {
                    if (string.IsNullOrEmpty(p.MeshAsset)) continue;
                    checkedCount++;
                    var meshPath = Path.Combine(contentPackPath, p.MeshAsset.Replace('/', Path.DirectorySeparatorChar));
                    if (!File.Exists(meshPath))
                    {
                        brokenCount++;
                        result.AddError("Mesh Refs", $"Prop '{p.Id}' references non-existent mesh: {p.MeshAsset}");
                    }
                }
            }
            catch { /* skip malformed */ }
        }

        if (brokenCount == 0 && checkedCount > 0)
            result.AddPass("Mesh Refs", $"{checkedCount} mesh references validated.");
    }

    private static void ValidateOrphanMeshes(string contentPackPath, ValidationResult result)
    {
        var meshesDir = Path.Combine(contentPackPath, "assets", "meshes");
        if (!Directory.Exists(meshesDir)) return;

        var glbFiles = Directory.EnumerateFiles(meshesDir, "*.glb", SearchOption.AllDirectories).ToList();
        var orphanCount = 0;

        foreach (var glb in glbFiles)
        {
            var metaPath = Path.ChangeExtension(glb, ".meta.json");
            if (!File.Exists(metaPath))
            {
                orphanCount++;
                var name = Path.GetFileName(glb);
                result.AddWarning("Orphan Meshes", $"{name} has no .meta.json.");
            }
        }

        if (orphanCount == 0 && glbFiles.Count > 0)
            result.AddPass("Orphan Meshes", $"All {glbFiles.Count} .glb files have .meta.json.");
    }

    private static void ValidateCatalog(string contentPackPath, ValidationResult result)
    {
        var catalogPath = Path.Combine(contentPackPath, "catalog.json");
        if (!File.Exists(catalogPath))
        {
            result.AddWarning("Catalog", "catalog.json does not exist.");
            return;
        }

        try
        {
            var catalog = JsonSerializer.Deserialize<CatalogFile>(
                File.ReadAllText(catalogPath), JsonOptions);
            if (catalog is not null)
            {
                var totalEntries = catalog.Building.Count + catalog.Prop.Count
                    + catalog.Surface.Count + catalog.TerrainMesh.Count;
                result.AddPass("Catalog", $"catalog.json has {totalEntries} entries.");
            }
        }
        catch (JsonException ex)
        {
            result.AddError("Catalog", $"catalog.json parse error: {ex.Message}");
        }
    }

    private static void ValidateScenarios(string contentPackPath, ValidationResult result)
    {
        var scenariosDir = Path.Combine(contentPackPath, "scenarios");
        if (!Directory.Exists(scenariosDir))
        {
            result.AddWarning("Scenarios", "scenarios/ directory does not exist.");
            return;
        }

        var scenarioFiles = Directory.GetFiles(scenariosDir, "*.json");
        if (scenarioFiles.Length == 0)
        {
            result.AddWarning("Scenarios", "No scenario files found.");
            return;
        }

        var townsDir = Path.Combine(contentPackPath, "towns");
        foreach (var file in scenarioFiles)
        {
            try
            {
                var doc = JsonDocument.Parse(File.ReadAllText(file));
                if (doc.RootElement.TryGetProperty("towns", out var towns) && towns.ValueKind == JsonValueKind.Array)
                {
                    foreach (var town in towns.EnumerateArray())
                    {
                        var townName = town.GetString();
                        if (townName is null) continue;
                        var townDir = Path.Combine(townsDir, townName);
                        if (!Directory.Exists(townDir))
                            result.AddError("Scenarios", $"Scenario '{Path.GetFileName(file)}' references non-existent town: {townName}");
                    }
                }

                result.AddPass("Scenarios", $"{Path.GetFileName(file)} is valid.");
            }
            catch (JsonException ex)
            {
                result.AddError("Scenarios", $"{Path.GetFileName(file)} parse error: {ex.Message}");
            }
        }
    }
}

// --- DTOs for JSON serialization ---

public sealed class ScenarioFile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public List<string> Towns { get; set; } = [];
    public PlayerStartInfo? PlayerStart { get; set; }
    public int Difficulty { get; set; } = 3;
    public List<string> Tags { get; set; } = [];
}

public sealed class PlayerStartInfo
{
    public string Town { get; set; } = "";
    public int TileX { get; set; }
    public int TileY { get; set; }
}

public sealed class ScenarioSettings
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public PlayerStartInfo? PlayerStart { get; set; }
    public int Difficulty { get; set; } = 3;
    public List<string> Tags { get; set; } = [];
}

public sealed class ManifestUpdate
{
    public string? Name { get; set; }
    public string? Version { get; set; }
    public string? Description { get; set; }
    public string? Author { get; set; }
}

public sealed class ManifestFile
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Version { get; set; } = "0.1.0";
    public string Description { get; set; } = "";
    public string Author { get; set; } = "";
    public string EngineVersion { get; set; } = ">=0.1.0";
    public string Parent { get; set; } = "";
    public List<string> Tags { get; set; } = [];
    public string DefaultScenario { get; set; } = "";
}

public sealed class CatalogFile
{
    public List<CatalogEntry> Building { get; set; } = [];
    public List<CatalogEntry> Prop { get; set; } = [];
    public List<CatalogEntry> Surface { get; set; } = [];

    [JsonPropertyName("terrain_mesh")]
    public List<CatalogEntry> TerrainMesh { get; set; } = [];
}

public sealed class CatalogEntry
{
    public string AssetId { get; set; } = "";
    public string Path { get; set; } = "";
}

internal sealed class MeshRefDto
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string MeshAsset { get; set; } = "";
}

internal sealed class MetaCategoryDto
{
    public string SourceType { get; set; } = "";
}
