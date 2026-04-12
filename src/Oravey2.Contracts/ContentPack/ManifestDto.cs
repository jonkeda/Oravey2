namespace Oravey2.Contracts.ContentPack;

public sealed record ManifestDto(
    string Id,
    string Name,
    string Version,
    string Description,
    string Author,
    string Parent,
    string? EngineVersion = null,
    string? RegionCode = null,
    string? DefaultScenario = null,
    List<string>? Tags = null);
