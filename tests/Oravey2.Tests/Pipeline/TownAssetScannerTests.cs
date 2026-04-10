using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class TownAssetScannerTests
{
    private static string CreateTempContentPack(
        params (string gameName, TownDesign design, TownMapResult? mapResult)[] towns)
    {
        var root = Path.Combine(Path.GetTempPath(), $"tas_test_{Guid.NewGuid():N}");
        foreach (var (gameName, design, mapResult) in towns)
        {
            var townDir = Path.Combine(root, "towns", gameName);
            Directory.CreateDirectory(townDir);
            TownDesignFile.FromTownDesign(design).Save(Path.Combine(townDir, "design.json"));

            if (mapResult is not null)
                TownMapFiles.Save(mapResult, townDir);
        }
        return root;
    }

    private static TownDesign MakeDesign(string townName) => new(
        townName,
        new LandmarkBuilding("The Beacon", "A tall lighthouse on a cliff", "large"),
        [
            new KeyLocation("Market Hall", "shop", "A bustling market building", "medium"),
            new KeyLocation("Clinic", "medical", "A small field clinic", "small"),
        ],
        "organic",
        []);

    private static TownMapResult MakeMapResult(string landmarkMesh = "", string marketMesh = "", string clinicMesh = "")
    {
        var buildings = new List<PlacedBuilding>
        {
            new("b_0", "The Beacon", landmarkMesh, "large", [[0, 0]], 3, 0.85f, new TilePlacement(0, 0, 0, 0)),
            new("b_1", "Market Hall", marketMesh, "medium", [[1, 1]], 1, 0.9f, new TilePlacement(0, 0, 1, 1)),
            new("b_2", "Clinic", clinicMesh, "small", [[2, 2]], 1, 0.7f, new TilePlacement(0, 0, 2, 2)),
        };
        var props = new List<PlacedProp>
        {
            new("p_0", "meshes/primitives/sphere.glb", new TilePlacement(0, 0, 3, 3), 0, 1, false),
        };
        return new TownMapResult(
            new TownLayout(16, 16, [[0]]),
            buildings, props,
            [new TownZone("z_0", "Main", 0, 0, 1, true, 0, 0, 0, 0)]);
    }

    [Fact]
    public void Scan_EmptyDirectory_ReturnsEmpty()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tas_test_{Guid.NewGuid():N}");
        try
        {
            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);
            Assert.Empty(result);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scan_DesignOnly_ReturnsUnplacedBuildings()
    {
        var root = CreateTempContentPack(("haven", MakeDesign("Island Haven"), null));
        try
        {
            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);

            Assert.Single(result);
            var town = result[0];
            Assert.Equal("Island Haven", town.TownName);
            Assert.Equal("haven", town.GameName);
            Assert.Equal(3, town.Buildings.Count);
            Assert.Equal("landmark", town.Buildings[0].Role);
            Assert.Equal("key", town.Buildings[1].Role);
            Assert.All(town.Buildings, b => Assert.Equal(MeshStatus.None, b.MeshStatus));
            Assert.Empty(town.Props);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scan_DesignWithMap_MergesPlacementData()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);

            Assert.Single(result);
            var town = result[0];
            Assert.Equal(3, town.Buildings.Count);
            Assert.Equal("b_0", town.Buildings[0].BuildingId);
            Assert.Equal(3, town.Buildings[0].Floors);
            Assert.Equal(0.85f, town.Buildings[0].Condition);
            Assert.Single(town.Props);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scan_PrimitiveMesh_ClassifiedAsPrimitive()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Island Haven"),
                MakeMapResult("meshes/primitives/pyramid.glb", "meshes/primitives/cube.glb", "")));
        try
        {
            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);

            var town = result[0];
            Assert.Equal(MeshStatus.Primitive, town.Buildings[0].MeshStatus);
            Assert.Equal(MeshStatus.Primitive, town.Buildings[1].MeshStatus);
            Assert.Equal(MeshStatus.None, town.Buildings[2].MeshStatus);
            Assert.Equal(MeshStatus.Primitive, town.Props[0].MeshStatus);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scan_RealMeshOnDisk_ClassifiedAsReady()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Island Haven"),
                MakeMapResult("meshes/the-beacon.glb")));
        try
        {
            // Create the actual .glb file
            var meshDir = Path.Combine(root, "assets", "meshes");
            Directory.CreateDirectory(meshDir);
            File.WriteAllBytes(Path.Combine(meshDir, "the-beacon.glb"), [0x01]);

            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);

            Assert.Equal(MeshStatus.Ready, result[0].Buildings[0].MeshStatus);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scan_MissingMeshFile_ClassifiedAsNone()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Island Haven"),
                MakeMapResult("meshes/nonexistent.glb")));
        try
        {
            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);

            Assert.Equal(MeshStatus.None, result[0].Buildings[0].MeshStatus);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scan_ExtraBuildingsInMap_AddedAsGeneric()
    {
        var design = new TownDesign(
            "Haven",
            new LandmarkBuilding("The Beacon", "A lighthouse", "large"),
            [new KeyLocation("Market", "shop", "Market stall", "medium")],
            "organic",
            []);

        var buildings = new List<PlacedBuilding>
        {
            new("b_0", "The Beacon", "", "large", [[0, 0]], 2, 0.5f, new TilePlacement(0, 0, 0, 0)),
            new("b_1", "Market", "", "medium", [[1, 1]], 1, 0.8f, new TilePlacement(0, 0, 1, 1)),
            new("b_2", "Ruin 1", "", "small", [[2, 2]], 1, 0.3f, new TilePlacement(0, 0, 2, 2)),
        };
        var mapResult = new TownMapResult(
            new TownLayout(16, 16, [[0]]),
            buildings, [],
            [new TownZone("z_0", "Main", 0, 0, 1, true, 0, 0, 0, 0)]);

        var root = CreateTempContentPack(("haven", design, mapResult));
        try
        {
            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);

            var town = result[0];
            Assert.Equal(3, town.Buildings.Count);
            Assert.Equal("landmark", town.Buildings[0].Role);
            Assert.Equal("key", town.Buildings[1].Role);
            Assert.Equal("generic", town.Buildings[2].Role);
            Assert.Equal("Ruin 1", town.Buildings[2].Name);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Scan_MultipleTowns_ReturnsAll()
    {
        var root = CreateTempContentPack(
            ("haven", MakeDesign("Island Haven"), MakeMapResult()),
            ("berg", MakeDesign("Havenburg"), null));
        try
        {
            var scanner = new TownAssetScanner();
            var result = scanner.Scan(root);

            Assert.Equal(2, result.Count);
            Assert.Contains(result, t => t.TownName == "Island Haven");
            Assert.Contains(result, t => t.TownName == "Havenburg");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void DeriveAssetId_NormalizesToLowerKebab()
    {
        Assert.Equal("island-haven-the-beacon",
            TownAssetScanner.DeriveAssetId("Island Haven", "The Beacon"));
    }

    [Fact]
    public void DeriveAssetId_HandlesUnderscores()
    {
        Assert.Equal("my-town-tall-tower",
            TownAssetScanner.DeriveAssetId("my_town", "tall_tower"));
    }
}
