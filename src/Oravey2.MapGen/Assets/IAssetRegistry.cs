namespace Oravey2.MapGen.Assets;

public sealed record AssetEntry(string Id, string Description, string[] Tags);

public interface IAssetRegistry
{
    IReadOnlyList<AssetEntry> Search(string assetType, string query);
    IReadOnlyList<AssetEntry> ListPrefabs(string category);
    bool Exists(string assetType, string id);
}
