using System.Numerics;
using Oravey2.Core.World.Vegetation;

namespace Oravey2.Tests.Vegetation;

public class CanopyClusterBuilderTests
{
    [Fact]
    public void FewTrees_SingleCluster()
    {
        var spawns = new List<TreeSpawn>
        {
            new(new Vector2(5f, 5f), TreeSpecies.Birch, 200, false),
            new(new Vector2(6f, 5f), TreeSpecies.Birch, 180, false),
            new(new Vector2(5f, 6f), TreeSpecies.Palm, 220, false),
            new(new Vector2(7f, 7f), TreeSpecies.Scrub, 190, false),
            new(new Vector2(6f, 6f), TreeSpecies.MutantWillow, 210, false),
        };

        var clusters = CanopyClusterBuilder.Build(spawns);

        Assert.Single(clusters);
        Assert.Equal(5, clusters[0].Trees.Count);
    }

    [Fact]
    public void ManySpreadTrees_MultipleClusters()
    {
        var spawns = new List<TreeSpawn>();

        // Group A: near origin
        for (int i = 0; i < 20; i++)
            spawns.Add(new TreeSpawn(new Vector2(i * 0.5f, i * 0.3f), TreeSpecies.Birch, 200, false));

        // Group B: far away (well beyond ClusterRadius of 12)
        for (int i = 0; i < 20; i++)
            spawns.Add(new TreeSpawn(new Vector2(50f + i * 0.5f, 50f + i * 0.3f), TreeSpecies.Palm, 200, false));

        var clusters = CanopyClusterBuilder.Build(spawns);

        Assert.True(clusters.Count >= 2, $"Expected 2+ clusters, got {clusters.Count}");
    }

    [Fact]
    public void NoTrees_NoCluster()
    {
        var clusters = CanopyClusterBuilder.Build(Array.Empty<TreeSpawn>());

        Assert.Empty(clusters);
    }
}
