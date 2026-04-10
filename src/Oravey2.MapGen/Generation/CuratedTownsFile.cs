using System.Text.Json;
using System.Text.Json.Serialization;

namespace Oravey2.MapGen.Generation;

public sealed class CuratedTownsFile
{
    public string Mode { get; set; } = "B";
    public int Seed { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public List<CuratedTownEntry> Towns { get; set; } = [];

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    public static CuratedTownsFile FromCuratedTowns(
        IEnumerable<CuratedTown> towns, string mode, int seed) => new()
    {
        Mode = mode,
        Seed = seed,
        GeneratedAt = DateTime.UtcNow,
        Towns = towns.Select(t => new CuratedTownEntry
        {
            GameName = t.GameName,
            RealName = t.RealName,
            Latitude = t.Latitude,
            Longitude = t.Longitude,
            Role = t.Role,
            Faction = t.Faction,
            ThreatLevel = t.ThreatLevel,
            Description = t.Description,
        }).ToList(),
    };

    public void Save(string path)
    {
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);
        File.WriteAllText(path, JsonSerializer.Serialize(this, SerializerOptions));
    }

    public static CuratedTownsFile Load(string path)
        => JsonSerializer.Deserialize<CuratedTownsFile>(
            File.ReadAllText(path), SerializerOptions) ?? new();
}

public sealed class CuratedTownEntry
{
    public string GameName { get; set; } = "";
    public string RealName { get; set; } = "";
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Role { get; set; } = "";
    public string Faction { get; set; } = "";
    public int ThreatLevel { get; set; }
    public string Description { get; set; } = "";
    public int EstimatedPopulation { get; set; }
}
