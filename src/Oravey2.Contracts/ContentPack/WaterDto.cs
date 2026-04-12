namespace Oravey2.Contracts.ContentPack;

public sealed record WaterDto(
    string Id,
    string WaterType,
    float[][] Geometry);
