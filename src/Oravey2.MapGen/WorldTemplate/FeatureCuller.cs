using System.Numerics;

namespace Oravey2.MapGen.WorldTemplate;

/// <summary>
/// Pure-function culling engine that reduces raw OSM features to a gameplay-relevant subset.
/// All methods return new lists without mutating the input.
/// </summary>
public static class FeatureCuller
{
    /// <summary>
    /// Cull towns by category, population, protected categories, spacing, and max count.
    /// </summary>
    public static List<TownEntry> CullTowns(List<TownEntry> towns, CullSettings settings)
    {
        if (towns.Count == 0) return [];

        // 1. Category filter
        var result = towns.Where(t => t.Category >= settings.TownMinCategory).ToList();

        // 2. Population filter
        result = result.Where(t => t.Population >= settings.TownMinPopulation).ToList();

        // 3. Protected categories — restore removed towns
        var protectedTowns = towns.Where(t =>
            !result.Contains(t) && IsProtected(t, settings)).ToList();
        result.AddRange(protectedTowns);

        // 4. Sort by priority
        result = SortByPriority(result, settings.TownPriority);

        // 5. Spacing enforcement (greedy)
        result = EnforceSpacing(result, settings.TownMinSpacingKm);

        // 6. Max cap
        if (result.Count > settings.TownMaxCount)
            result = result.Take(settings.TownMaxCount).ToList();

        return result;
    }

    /// <summary>
    /// Cull roads by class, motorway protection, town proximity, dead-end removal, and geometry simplification.
    /// </summary>
    public static List<RoadSegment> CullRoads(
        List<RoadSegment> roads,
        List<TownEntry> includedTowns,
        CullSettings settings)
    {
        if (roads.Count == 0) return [];

        // 1. Class filter
        var result = roads.Where(r => r.RoadClass <= settings.RoadMinClass).ToList();

        // 2. Motorway protection
        if (settings.RoadAlwaysKeepMotorways)
        {
            var motorways = roads.Where(r => r.RoadClass == RoadClass.Motorway && !result.Contains(r));
            result.AddRange(motorways);
        }

        // 3. Town proximity — include roads near included towns even if filtered by class
        if (settings.RoadKeepNearTowns && includedTowns.Count > 0)
        {
            double proximityMetres = settings.RoadTownProximityKm * 1000.0;
            var nearbyRoads = roads.Where(r =>
                !result.Contains(r) &&
                IsRoadNearAnyTown(r, includedTowns, proximityMetres));
            result.AddRange(nearbyRoads);
        }

        // 4. Dead-end removal
        if (settings.RoadRemoveDeadEnds)
        {
            double minLengthMetres = settings.RoadDeadEndMinKm * 1000.0;
            result = RemoveDeadEnds(result, minLengthMetres);
        }

        // 5. Geometry simplification
        if (settings.RoadSimplifyGeometry)
        {
            result = result.Select(r =>
            {
                var simplified = SimplifyLine(r.Nodes, settings.RoadSimplifyToleranceM);
                return new RoadSegment(r.RoadClass, simplified);
            }).ToList();
        }

        return result;
    }

    /// <summary>
    /// Cull water bodies by computed area, river length, and protected types.
    /// Area and length are computed from the Geometry (game-space metres).
    /// </summary>
    public static List<WaterBody> CullWater(List<WaterBody> water, CullSettings settings)
    {
        if (water.Count == 0) return [];

        var result = new List<WaterBody>();
        double minAreaM2 = settings.WaterMinAreaKm2 * 1_000_000.0;
        double minLengthM = settings.WaterMinRiverLengthKm * 1000.0;

        foreach (var w in water)
        {
            bool keep;
            if (w.Type == WaterType.River || w.Type == WaterType.Canal)
            {
                double length = ComputePolylineLength(w.Geometry);
                keep = length >= minLengthM;
            }
            else
            {
                double area = ComputePolygonArea(w.Geometry);
                keep = area >= minAreaM2;
            }

            // Protected types override
            if (!keep)
            {
                if (settings.WaterAlwaysKeepSea && w.Type == WaterType.Sea) keep = true;
                if (settings.WaterAlwaysKeepLakes && w.Type == WaterType.Lake) keep = true;
            }

            if (keep) result.Add(w);
        }

        return result;
    }

    /// <summary>
    /// Douglas-Peucker line simplification. Tolerance is in the same units as the points (game-space metres).
    /// </summary>
    public static Vector2[] SimplifyLine(Vector2[] points, double tolerance)
    {
        if (points.Length <= 2) return points;

        var keep = new bool[points.Length];
        keep[0] = true;
        keep[points.Length - 1] = true;

        DouglasPeuckerRecursive(points, 0, points.Length - 1, tolerance, keep);

        var result = new List<Vector2>();
        for (int i = 0; i < points.Length; i++)
        {
            if (keep[i]) result.Add(points[i]);
        }

        // Ensure at least 2 points
        if (result.Count < 2)
            return [points[0], points[^1]];

        return result.ToArray();
    }

    // --- Private helpers ---

    private static bool IsProtected(TownEntry town, CullSettings settings)
    {
        if (settings.TownAlwaysKeepCities && town.Category == TownCategory.City) return true;
        if (settings.TownAlwaysKeepMetropolis && town.Category == TownCategory.Metropolis) return true;
        return false;
    }

    private static List<TownEntry> SortByPriority(List<TownEntry> towns, CullPriority priority)
    {
        return priority switch
        {
            CullPriority.Population => towns.OrderByDescending(t => t.Population).ToList(),
            CullPriority.Category => towns.OrderByDescending(t => t.Category)
                                          .ThenByDescending(t => t.Population).ToList(),
            CullPriority.Spacing => towns.OrderByDescending(t => t.Population).ToList(),
            _ => towns
        };
    }

    private static List<TownEntry> EnforceSpacing(List<TownEntry> sorted, double minSpacingKm)
    {
        double minDistM = minSpacingKm * 1000.0;
        double minDistSq = minDistM * minDistM;
        var kept = new List<TownEntry>();

        foreach (var town in sorted)
        {
            bool tooClose = false;
            foreach (var existing in kept)
            {
                double dx = town.GamePosition.X - existing.GamePosition.X;
                double dz = town.GamePosition.Y - existing.GamePosition.Y;
                if (dx * dx + dz * dz < minDistSq)
                {
                    tooClose = true;
                    break;
                }
            }
            if (!tooClose) kept.Add(town);
        }

        return kept;
    }

    private static bool IsRoadNearAnyTown(RoadSegment road, List<TownEntry> towns, double proximityMetres)
    {
        double proxSq = proximityMetres * proximityMetres;
        foreach (var node in road.Nodes)
        {
            foreach (var town in towns)
            {
                double dx = node.X - town.GamePosition.X;
                double dz = node.Y - town.GamePosition.Y;
                if (dx * dx + dz * dz <= proxSq) return true;
            }
        }
        return false;
    }

    private static List<RoadSegment> RemoveDeadEnds(List<RoadSegment> roads, double minLengthMetres)
    {
        const float endpointTolerance = 5f; // metres — snap endpoints together
        var toleranceSq = endpointTolerance * endpointTolerance;

        // Build endpoint degree map
        var endpointDegree = new Dictionary<int, int>(); // road index → endpoint connectivity count
        for (int i = 0; i < roads.Count; i++)
        {
            int connections = 0;
            var start = roads[i].Nodes[0];
            var end = roads[i].Nodes[^1];

            for (int j = 0; j < roads.Count; j++)
            {
                if (i == j) continue;
                var otherStart = roads[j].Nodes[0];
                var otherEnd = roads[j].Nodes[^1];

                if (DistanceSq(start, otherStart) < toleranceSq ||
                    DistanceSq(start, otherEnd) < toleranceSq)
                    connections++;
                if (DistanceSq(end, otherStart) < toleranceSq ||
                    DistanceSq(end, otherEnd) < toleranceSq)
                    connections++;
            }

            endpointDegree[i] = connections;
        }

        return roads.Where((road, idx) =>
        {
            // Only remove if it's a dead-end (one or zero connections) AND short
            if (endpointDegree[idx] >= 2) return true; // well-connected, keep
            double length = ComputePolylineLength(road.Nodes);
            return length >= minLengthMetres;
        }).ToList();
    }

    internal static double ComputePolylineLength(Vector2[] points)
    {
        double length = 0;
        for (int i = 1; i < points.Length; i++)
        {
            double dx = points[i].X - points[i - 1].X;
            double dy = points[i].Y - points[i - 1].Y;
            length += Math.Sqrt(dx * dx + dy * dy);
        }
        return length;
    }

    internal static double ComputePolygonArea(Vector2[] points)
    {
        // Shoelface formula — works for game-space coordinates (metres)
        if (points.Length < 3) return 0;

        double area = 0;
        for (int i = 0; i < points.Length; i++)
        {
            var j = (i + 1) % points.Length;
            area += points[i].X * points[j].Y;
            area -= points[j].X * points[i].Y;
        }
        return Math.Abs(area) / 2.0;
    }

    private static void DouglasPeuckerRecursive(Vector2[] points, int start, int end, double tolerance, bool[] keep)
    {
        if (end - start < 2) return;

        double maxDist = 0;
        int maxIdx = start;

        var lineStart = points[start];
        var lineEnd = points[end];

        for (int i = start + 1; i < end; i++)
        {
            double dist = PerpendicularDistance(points[i], lineStart, lineEnd);
            if (dist > maxDist)
            {
                maxDist = dist;
                maxIdx = i;
            }
        }

        if (maxDist > tolerance)
        {
            keep[maxIdx] = true;
            DouglasPeuckerRecursive(points, start, maxIdx, tolerance, keep);
            DouglasPeuckerRecursive(points, maxIdx, end, tolerance, keep);
        }
    }

    private static double PerpendicularDistance(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        double dx = lineEnd.X - lineStart.X;
        double dy = lineEnd.Y - lineStart.Y;
        double lengthSq = dx * dx + dy * dy;

        if (lengthSq < 1e-10)
            return Math.Sqrt(DistanceSq(point, lineStart));

        double cross = Math.Abs((point.X - lineStart.X) * dy - (point.Y - lineStart.Y) * dx);
        return cross / Math.Sqrt(lengthSq);
    }

    private static double DistanceSq(Vector2 a, Vector2 b)
    {
        double dx = a.X - b.X;
        double dy = a.Y - b.Y;
        return dx * dx + dy * dy;
    }
}
