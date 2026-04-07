using System.Numerics;

namespace Oravey2.Core.World.Vegetation;

/// <summary>
/// Groups nearby trees into canopy clusters for Level 2 LOD rendering.
/// Each cluster merges ~20 trees in a circular area into one blob mesh.
/// </summary>
public static class CanopyClusterBuilder
{
    /// <summary>Maximum radius (in world units) for grouping trees into a single cluster.</summary>
    public const float ClusterRadius = 12f;

    /// <summary>
    /// Builds canopy clusters from a set of tree spawns using a simple greedy clustering algorithm.
    /// Trees that are dead are excluded from canopy clusters.
    /// </summary>
    public static IReadOnlyList<CanopyCluster> Build(IReadOnlyList<TreeSpawn> spawns)
    {
        if (spawns.Count == 0)
            return Array.Empty<CanopyCluster>();

        // Filter to living trees only (dead trees have no canopy)
        var living = new List<TreeSpawn>();
        foreach (var s in spawns)
        {
            if (!s.IsDead)
                living.Add(s);
        }

        if (living.Count == 0)
            return Array.Empty<CanopyCluster>();

        var assigned = new bool[living.Count];
        var clusters = new List<CanopyCluster>();

        for (int i = 0; i < living.Count; i++)
        {
            if (assigned[i]) continue;

            // Start a new cluster from this tree
            var members = new List<TreeSpawn> { living[i] };
            assigned[i] = true;

            // Greedily add nearby unassigned trees
            for (int j = i + 1; j < living.Count; j++)
            {
                if (assigned[j]) continue;

                float dist = Vector2.Distance(living[i].Position, living[j].Position);
                if (dist <= ClusterRadius)
                {
                    members.Add(living[j]);
                    assigned[j] = true;
                }
            }

            // Compute cluster center as average position
            var center = Vector2.Zero;
            foreach (var m in members)
                center += m.Position;
            center /= members.Count;

            // Compute cluster radius as max distance from center + canopy radius
            float maxDist = 0f;
            foreach (var m in members)
            {
                float d = Vector2.Distance(center, m.Position);
                if (d > maxDist) maxDist = d;
            }

            clusters.Add(new CanopyCluster(center, maxDist + 1f, members));
        }

        return clusters;
    }
}

/// <summary>
/// A group of nearby trees whose canopies can be merged into a single blob mesh for L2 rendering.
/// </summary>
public sealed class CanopyCluster
{
    public Vector2 Center { get; }
    public float Radius { get; }
    public IReadOnlyList<TreeSpawn> Trees { get; }

    public CanopyCluster(Vector2 center, float radius, IReadOnlyList<TreeSpawn> trees)
    {
        Center = center;
        Radius = radius;
        Trees = trees;
    }
}
