namespace Oravey2.Core.World;

public sealed class BuildingRegistry
{
    private readonly Dictionary<string, BuildingDefinition> _buildings = new();
    private readonly Dictionary<(int, int), List<BuildingDefinition>> _byChunk = new();

    public void Register(BuildingDefinition building)
    {
        if (!_buildings.TryAdd(building.Id, building))
            throw new ArgumentException($"Duplicate building ID: '{building.Id}'");
    }

    public void RegisterForChunk(BuildingDefinition building, int chunkX, int chunkY)
    {
        Register(building);
        var key = (chunkX, chunkY);
        if (!_byChunk.TryGetValue(key, out var list))
        {
            list = new List<BuildingDefinition>();
            _byChunk[key] = list;
        }
        list.Add(building);
    }

    public BuildingDefinition? GetById(string id)
        => _buildings.GetValueOrDefault(id);

    public IReadOnlyList<BuildingDefinition> GetByChunk(int chunkX, int chunkY)
        => _byChunk.TryGetValue((chunkX, chunkY), out var list)
            ? list
            : Array.Empty<BuildingDefinition>();

    public IReadOnlyList<BuildingDefinition> GetAll()
        => _buildings.Values.ToList();
}
