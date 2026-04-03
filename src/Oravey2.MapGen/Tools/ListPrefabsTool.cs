using System.Text.Json;
using Oravey2.MapGen.Assets;

namespace Oravey2.MapGen.Tools;

public sealed class ListPrefabsTool
{
    private readonly IAssetRegistry _registry;

    public ListPrefabsTool(IAssetRegistry registry)
    {
        _registry = registry;
    }

    public string Handle(string category)
    {
        var prefabs = _registry.ListPrefabs(category);

        return JsonSerializer.Serialize(new
        {
            category,
            count = prefabs.Count,
            prefabs = prefabs.Select(p => new { p.Id, p.Description, p.Tags }).ToArray()
        });
    }
}
