using System.Text.Json;
using Oravey2.MapGen.Assets;

namespace Oravey2.MapGen.Tools;

public sealed class LookupAssetTool
{
    private readonly IAssetRegistry _registry;

    public LookupAssetTool(IAssetRegistry registry)
    {
        _registry = registry;
    }

    public string Handle(string assetType, string query)
    {
        var results = _registry.Search(assetType, query);

        return JsonSerializer.Serialize(new
        {
            count = results.Count,
            assets = results.Select(a => new { a.Id, a.Description, a.Tags }).ToArray()
        });
    }
}
