namespace Oravey2.Contracts.ContentPack;

public sealed record RoadDto(
    string Id,
    string RoadClass,
    float[][] Nodes,
    string? FromTown,
    string? ToTown);
