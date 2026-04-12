using Oravey2.Core.Data;

namespace Oravey2.Core.Content;

/// <summary>
/// A region available for import from a content pack's world.db.
/// </summary>
public sealed record ImportableRegion(
    string PackId,
    string PackName,
    string RegionName,
    string Description,
    string PackDirectory);

/// <summary>
/// Bridges discovered content packs to the user's persistent world.db.
/// Lists which packs have a world.db ready to import and performs the import.
/// </summary>
public sealed class ContentPackImportService
{
    private readonly ContentPackService _packs;

    public ContentPackImportService(ContentPackService packs)
    {
        _packs = packs;
    }

    /// <summary>
    /// Returns content packs that have a world.db ready to import.
    /// Opens each pack's world.db read-only to list regions.
    /// </summary>
    public IReadOnlyList<ImportableRegion> GetImportableRegions()
    {
        var regions = new List<ImportableRegion>();
        foreach (var pack in _packs.Packs)
        {
            var packDbPath = WorldDbPaths.GetPackWorldDbPath(pack.Directory);
            if (packDbPath == null) continue;

            using var packStore = new WorldMapStore(packDbPath);
            foreach (var region in packStore.GetAllRegions())
            {
                regions.Add(new ImportableRegion(
                    PackId: pack.Manifest.Id,
                    PackName: pack.Manifest.Name,
                    RegionName: region.Name,
                    Description: region.Description ?? pack.Manifest.Description ?? "",
                    PackDirectory: pack.Directory));
            }
        }
        return regions;
    }

    /// <summary>
    /// Imports all regions from a content pack into the user's world.db.
    /// Uses upsert — re-importing replaces existing regions.
    /// </summary>
    public ImportResult ImportRegion(string contentPackDir)
    {
        var userDbPath = WorldDbPaths.GetUserWorldDbPath();
        using var userStore = new WorldMapStore(userDbPath);
        var importer = new ContentPackImporter(userStore);
        return importer.Import(contentPackDir);
    }

    /// <summary>
    /// Checks whether a region is already imported in the user's world.db.
    /// </summary>
    public bool IsRegionImported(string regionName)
    {
        var userDbPath = WorldDbPaths.GetUserWorldDbPath();
        if (!File.Exists(userDbPath)) return false;
        using var store = new WorldMapStore(userDbPath);
        return store.GetRegionByName(regionName) != null;
    }
}
