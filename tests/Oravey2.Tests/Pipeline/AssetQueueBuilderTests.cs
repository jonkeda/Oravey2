using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class AssetQueueBuilderTests
{
    private static string CreateTempContentPack(params (string gameName, TownDesign design)[] towns)
    {
        var root = Path.Combine(Path.GetTempPath(), $"aqb_test_{Guid.NewGuid():N}");
        foreach (var (gameName, design) in towns)
        {
            var townDir = Path.Combine(root, "towns", gameName);
            Directory.CreateDirectory(townDir);
            TownDesignFile.FromTownDesign(design).Save(Path.Combine(townDir, "design.json"));
        }
        return root;
    }

    private static TownDesign MakeDesign(string townName, string landmarkDesc = "A tall tower",
        params (string name, string desc)[] keyLocations) => new(
        townName,
        new LandmarkBuilding("The Beacon", landmarkDesc, "large"),
        keyLocations.Select(k => new KeyLocation(k.name, "purpose", k.desc, "medium")).ToList(),
        "organic",
        []);

    [Fact]
    public void BuildQueue_SingleTown_ReturnsLandmarkAndLocations()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Island Haven", "A lighthouse",
                ("Market", "A bustling market"))));
        try
        {
            var builder = new AssetQueueBuilder();
            var queue = builder.BuildQueue(root);

            Assert.Equal(2, queue.Count);
            Assert.Contains(queue, a => a.LocationName == "The Beacon");
            Assert.Contains(queue, a => a.LocationName == "Market");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildQueue_DeduplicatesByVisualDescription()
    {
        var root = CreateTempContentPack(
            ("town_a", MakeDesign("Town A", "A tall tower")),
            ("town_b", MakeDesign("Town B", "A tall tower"))); // same description
        try
        {
            var builder = new AssetQueueBuilder();
            var queue = builder.BuildQueue(root);

            // Only one asset for "A tall tower" despite two towns
            var towerAssets = queue.Where(a => a.VisualDescription == "A tall tower").ToList();
            Assert.Single(towerAssets);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildQueue_SkipsEmptyDescriptions()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Haven", "A lighthouse",
                ("Empty", "")))); // empty visual description
        try
        {
            var builder = new AssetQueueBuilder();
            var queue = builder.BuildQueue(root);

            Assert.Single(queue); // only the landmark
            Assert.Equal("The Beacon", queue[0].LocationName);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildQueue_DetectsExistingGlb()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Haven", "A lighthouse")));
        try
        {
            // Create a .glb file for the landmark
            var meshDir = Path.Combine(root, "assets", "meshes");
            Directory.CreateDirectory(meshDir);
            var assetId = AssetQueueBuilder.DeriveAssetId("Haven", "The Beacon");
            File.WriteAllBytes(Path.Combine(meshDir, $"{assetId}.glb"), [0x01]);

            var builder = new AssetQueueBuilder();
            var queue = builder.BuildQueue(root);

            Assert.Single(queue);
            Assert.Equal(AssetStatus.Ready, queue[0].Status);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildQueue_EmptyDirectory_ReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), $"aqb_test_{Guid.NewGuid():N}");
        try
        {
            var builder = new AssetQueueBuilder();
            var queue = builder.BuildQueue(root);
            Assert.Empty(queue);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildQueue_PendingByDefault()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Haven", "A lighthouse")));
        try
        {
            var builder = new AssetQueueBuilder();
            var queue = builder.BuildQueue(root);

            Assert.All(queue, a => Assert.Equal(AssetStatus.Pending, a.Status));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DeriveAssetId_NormalizesNameToLowerKebab()
    {
        var id = AssetQueueBuilder.DeriveAssetId("Island Haven", "The Beacon");
        Assert.Equal("island-haven-the-beacon", id);
    }

    [Fact]
    public void DeriveAssetId_HandlesUnderscores()
    {
        var id = AssetQueueBuilder.DeriveAssetId("my_town", "tall_tower");
        Assert.Equal("my-town-tall-tower", id);
    }
}
