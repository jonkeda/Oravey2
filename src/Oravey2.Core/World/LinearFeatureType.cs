namespace Oravey2.Core.World;

public enum LinearFeatureType : byte
{
    // Roads (from OSM classification, ordered by importance)
    Path = 0,
    Residential = 1,
    Tertiary = 2,
    Secondary = 3,
    Primary = 4,
    Trunk = 5,
    Motorway = 6,

    // Rail
    Rail = 10,

    // Water
    Stream = 20,
    River = 21,
    Canal = 22,

    // Infrastructure
    Pipeline = 30,
}
