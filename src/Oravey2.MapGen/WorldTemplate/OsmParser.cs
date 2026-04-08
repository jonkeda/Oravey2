using System.Numerics;
using OsmSharp;
using OsmSharp.Streams;

namespace Oravey2.MapGen.WorldTemplate;

/// <summary>
/// Parses OpenStreetMap PBF files and extracts towns, roads, water, railways, and land use data.
/// All geometries are converted to game coordinates via <see cref="GeoMapper"/>.
/// </summary>
public class OsmParser
{
    private readonly GeoMapper _geoMapper;

    public OsmParser(GeoMapper geoMapper)
    {
        _geoMapper = geoMapper;
    }

    public OsmExtract ParsePbf(string pbfFilePath)
    {
        using var fileStream = File.OpenRead(pbfFilePath);
        return ParsePbf(fileStream);
    }

    public OsmExtract ParsePbf(Stream stream)
    {
        var source = new PBFOsmStreamSource(stream);
        var nodes = new Dictionary<long, (double Lat, double Lon)>();
        var wayNodes = new Dictionary<long, long[]>();
        var wayTags = new Dictionary<long, Dictionary<string, string>>();

        var towns = new List<TownEntry>();
        var roads = new List<RoadSegment>();
        var waterBodies = new List<WaterBody>();
        var railways = new List<RailwaySegment>();
        var landUseZones = new List<LandUseZone>();

        foreach (var osmGeo in source)
        {
            switch (osmGeo)
            {
                case Node node when node.Id.HasValue && node.Latitude.HasValue && node.Longitude.HasValue:
                    nodes[node.Id.Value] = (node.Latitude.Value, node.Longitude.Value);
                    TryExtractTown(node, towns);
                    break;

                case Way way when way.Id.HasValue && way.Nodes != null && way.Tags != null:
                    wayNodes[way.Id.Value] = way.Nodes;
                    wayTags[way.Id.Value] = way.Tags.ToDictionary(t => t.Key, t => t.Value);
                    break;
            }
        }

        // Second pass: resolve ways to coordinates
        foreach (var (wayId, tags) in wayTags)
        {
            if (!wayNodes.TryGetValue(wayId, out var nodeIds)) continue;
            var coords = ResolveNodes(nodeIds, nodes);
            if (coords.Length < 2) continue;

            if (tags.TryGetValue("highway", out var highway))
            {
                var roadClass = ClassifyRoad(highway);
                if (roadClass.HasValue)
                    roads.Add(new RoadSegment(roadClass.Value, coords));
            }

            if (tags.TryGetValue("waterway", out _) || (tags.TryGetValue("natural", out var natural) && natural == "water"))
            {
                var waterType = ClassifyWater(tags);
                waterBodies.Add(new WaterBody(waterType, coords));
            }

            if (tags.TryGetValue("railway", out var railway) && railway == "rail")
            {
                railways.Add(new RailwaySegment(coords));
            }

            if (tags.TryGetValue("landuse", out var landuse))
            {
                var luType = ClassifyLandUse(landuse);
                landUseZones.Add(new LandUseZone(luType, coords));
            }
        }

        return new OsmExtract(towns, roads, waterBodies, railways, landUseZones);
    }

    private void TryExtractTown(Node node, List<TownEntry> towns)
    {
        if (node.Tags == null) return;
        if (!node.Tags.TryGetValue("place", out var place)) return;

        var category = place switch
        {
            "city" => TownCategory.City,
            "town" => TownCategory.Town,
            "village" => TownCategory.Village,
            "hamlet" => TownCategory.Hamlet,
            _ => (TownCategory?)null
        };

        if (category == null) return;

        string name = node.Tags.TryGetValue("name", out var n) ? n : "Unknown";
        int population = node.Tags.TryGetValue("population", out var pop) && int.TryParse(pop, out var p) ? p : 0;
        var gamePos = _geoMapper.LatLonToGameXZ(node.Latitude!.Value, node.Longitude!.Value);

        towns.Add(new TownEntry(name, node.Latitude!.Value, node.Longitude!.Value, population, gamePos, category.Value));
    }

    private Vector2[] ResolveNodes(long[] nodeIds, Dictionary<long, (double Lat, double Lon)> nodeMap)
    {
        var coords = new List<Vector2>(nodeIds.Length);
        foreach (var id in nodeIds)
        {
            if (nodeMap.TryGetValue(id, out var latlon))
                coords.Add(_geoMapper.LatLonToGameXZ(latlon.Lat, latlon.Lon));
        }
        return coords.ToArray();
    }

    private static RoadClass? ClassifyRoad(string highway) => highway switch
    {
        "motorway" or "motorway_link" => RoadClass.Motorway,
        "trunk" or "trunk_link" => RoadClass.Trunk,
        "primary" or "primary_link" => RoadClass.Primary,
        "secondary" or "secondary_link" => RoadClass.Secondary,
        "tertiary" or "tertiary_link" => RoadClass.Tertiary,
        "residential" or "living_street" => RoadClass.Residential,
        _ => null
    };

    private static WaterType ClassifyWater(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("waterway", out var ww))
        {
            return ww switch
            {
                "river" => WaterType.River,
                "canal" => WaterType.Canal,
                _ => WaterType.River
            };
        }
        return WaterType.Lake;
    }

    private static LandUseType ClassifyLandUse(string landuse) => landuse switch
    {
        "forest" => LandUseType.Forest,
        "farmland" => LandUseType.Farmland,
        "residential" => LandUseType.Residential,
        "industrial" => LandUseType.Industrial,
        "commercial" => LandUseType.Commercial,
        "meadow" => LandUseType.Meadow,
        "orchard" => LandUseType.Orchard,
        "cemetery" => LandUseType.Cemetery,
        "military" => LandUseType.Military,
        _ => LandUseType.Other
    };
}

public record OsmExtract(
    List<TownEntry> Towns,
    List<RoadSegment> Roads,
    List<WaterBody> WaterBodies,
    List<RailwaySegment> Railways,
    List<LandUseZone> LandUseZones);
