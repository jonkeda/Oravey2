using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.MapGen.Generation;

public sealed class CuratedTownsFile
{
    public string Mode { get; set; } = "B";
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<CuratedTownDto> Towns { get; set; } = [];

    public static CuratedTownsFile FromCuratedTowns(
        IEnumerable<CuratedTown> towns, string mode) => new()
    {
        Mode = mode,
        GeneratedAt = DateTime.UtcNow,
        Towns = towns.Select(t => new CuratedTownDto(
            t.GameName, t.RealName, t.Latitude, t.Longitude,
            t.Description, t.Size.ToString(), t.Inhabitants, t.Destruction.ToString())).ToList(),
    };

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, ContentPackSerializer.WriteOptions));
    }

    public static CuratedTownsFile Load(string path)
        => JsonSerializer.Deserialize<CuratedTownsFile>(
            File.ReadAllText(path), ContentPackSerializer.ReadOptions) ?? new();
}
