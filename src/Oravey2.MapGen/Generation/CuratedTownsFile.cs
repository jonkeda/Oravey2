using System.Text.Json;
using Oravey2.Contracts;
using Oravey2.Contracts.ContentPack;

namespace Oravey2.MapGen.Generation;

public sealed class CuratedTownsFile
{
    public string Mode { get; set; } = "B";
    public int Seed { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<CuratedTownDto> Towns { get; set; } = [];

    public static CuratedTownsFile FromCuratedTowns(
        IEnumerable<CuratedTown> towns, string mode, int seed) => new()
    {
        Mode = mode,
        Seed = seed,
        GeneratedAt = DateTime.UtcNow,
        Towns = towns.Select(t => new CuratedTownDto(
            t.GameName, t.RealName, t.Latitude, t.Longitude,
            t.Role, t.Faction, t.ThreatLevel, t.Description, 0)).ToList(),
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
