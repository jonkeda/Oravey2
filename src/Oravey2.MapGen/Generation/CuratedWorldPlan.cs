namespace Oravey2.MapGen.Generation;

public sealed record CuratedWorldPlan(
    string WorldName,
    int Seed,
    List<CuratedRegion> Regions);
