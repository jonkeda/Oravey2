using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.Generation;

public sealed class RoadSelector
{
    public List<LinearFeatureData> Select(
        RegionTemplate region,
        CuratedRegion curated)
    {
        var curatedPositions = curated.Towns.Select(t => t.GamePosition).ToHashSet();
        var result = new List<LinearFeatureData>();

        foreach (var road in region.Roads)
        {
            // Keep all motorways
            if (road.RoadClass == RoadClass.Motorway)
            {
                result.Add(ToFeature(road));
                continue;
            }

            // Keep roads that connect to curated towns
            if (ConnectsToCuratedTown(road, curatedPositions))
            {
                var smoothed = CatmullRomSmooth(road.Nodes);
                result.Add(new LinearFeatureData(
                    MapRoadClass(road.RoadClass),
                    RoadWidth(road.RoadClass),
                    smoothed));
            }
        }

        return result;
    }

    private static bool ConnectsToCuratedTown(RoadSegment road, HashSet<Vector2> curatedPositions)
    {
        const float proximityThreshold = 500f; // metres
        foreach (var node in road.Nodes)
        {
            foreach (var townPos in curatedPositions)
            {
                if (Vector2.Distance(node, townPos) < proximityThreshold)
                    return true;
            }
        }
        return false;
    }

    internal static Vector2[] CatmullRomSmooth(Vector2[] points, int subdivisions = 4)
    {
        if (points.Length < 3) return points;

        var result = new List<Vector2> { points[0] };
        for (int i = 0; i < points.Length - 1; i++)
        {
            var p0 = points[Math.Max(0, i - 1)];
            var p1 = points[i];
            var p2 = points[Math.Min(points.Length - 1, i + 1)];
            var p3 = points[Math.Min(points.Length - 1, i + 2)];

            for (int s = 1; s <= subdivisions; s++)
            {
                float t = s / (float)subdivisions;
                result.Add(CatmullRomPoint(p0, p1, p2, p3, t));
            }
        }
        return result.ToArray();
    }

    private static Vector2 CatmullRomPoint(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, float t)
    {
        float t2 = t * t;
        float t3 = t2 * t;
        return 0.5f * (
            (2f * p1) +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }

    private static LinearFeatureData ToFeature(RoadSegment road) =>
        new(MapRoadClass(road.RoadClass), RoadWidth(road.RoadClass), road.Nodes);

    private static LinearFeatureType MapRoadClass(RoadClass rc) => rc switch
    {
        RoadClass.Motorway => LinearFeatureType.Highway,
        RoadClass.Trunk => LinearFeatureType.Road,
        RoadClass.Primary => LinearFeatureType.Road,
        _ => LinearFeatureType.DirtRoad
    };

    private static float RoadWidth(RoadClass rc) => rc switch
    {
        RoadClass.Motorway => 12f,
        RoadClass.Trunk => 8f,
        RoadClass.Primary => 6f,
        _ => 4f
    };
}
