using Oravey2.Contracts.ContentPack;
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
            design.Save(Path.Combine(townDir, "design.json"));

            if (mapResult is not null)
                TownMapFiles.Save(mapResult, townDir);
        }
        return root;
    }

    private static TownDesign MakeDesign(string townName) => new()
    {
        TownName = townName,
        Landmarks = [new LandmarkBuilding { Name = "The Beacon", VisualDescription = "A tall lighthouse on a cliff", SizeCategory = "large", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" }],
        KeyLocations =
        [
            new KeyLocation { Name = "Market Hall", Purpose = "shop", VisualDescription = "A bustling market building", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
            new KeyLocation { Name = "Clinic", Purpose = "medical", VisualDescription = "A small field clinic", SizeCategory = "small", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" },
        ],
        LayoutStyle = "organic",
        Hazards = [],
    };

    private static TownMapResult MakeMapResult(string landmarkMesh = "", string marketMesh = "", string clinicMesh = "")
    {
        var buildings = new List<BuildingDto>
        {
            new() { Id = "b_0", Name = "The Beacon", MeshAsset = landmarkMesh, Size = "large", Footprint = [[0, 0]], Floors = 3, Condition = 0.85f, Placement = new PlacementDto(0, 0, 0, 0) },
            new() { Id = "b_1", Name = "Market Hall", MeshAsset = marketMesh, Size = "medium", Footprint = [[1, 1]], Floors = 1, Condition = 0.9f, Placement = new PlacementDto(0, 0, 1, 1) },
            new() { Id = "b_2", Name = "Clinic", MeshAsset = clinicMesh, Size = "small", Footprint = [[2, 2]], Floors = 1, Condition = 0.7f, Placement = new PlacementDto(0, 0, 2, 2) },
        };
        var props = new List<PropDto>
        {
            new() { Id = "p_0", MeshAsset = "meshes/primitives/sphere.glb", Placement = new PlacementDto(0, 0, 3, 3), Rotation = 0, Scale = 1, BlocksWalkability = false },
        };
        return new TownMapResult
        {
            Layout = new LayoutDto { Width = 16, Height = 16, Surface = [[0]] },
            Buildings = buildings, Props = props,
            Zones = [new ZoneDto { Id = "z_0", Name = "Main", Biome = 0, RadiationLevel = 0, EnemyDifficultyTier = 1, IsFastTravelTarget = true, ChunkStartX = 0, ChunkStartY = 0, ChunkEndX = 0, ChunkEndY = 0 }],
        };
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
        var design = new TownDesign
        {
            TownName = "Haven",
            Landmarks = [new LandmarkBuilding { Name = "The Beacon", VisualDescription = "A lighthouse", SizeCategory = "large", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" }],
            KeyLocations = [new KeyLocation { Name = "Market", Purpose = "shop", VisualDescription = "Market stall", SizeCategory = "medium", OriginalDescription = "", MeshyPrompt = "", PositionHint = "" }],
            LayoutStyle = "organic",
            Hazards = [],
        };

        var buildings = new List<BuildingDto>
        {
            new() { Id = "b_0", Name = "The Beacon", MeshAsset = "", Size = "large", Footprint = [[0, 0]], Floors = 2, Condition = 0.5f, Placement = new PlacementDto(0, 0, 0, 0) },
            new() { Id = "b_1", Name = "Market", MeshAsset = "", Size = "medium", Footprint = [[1, 1]], Floors = 1, Condition = 0.8f, Placement = new PlacementDto(0, 0, 1, 1) },
            new() { Id = "b_2", Name = "Ruin 1", MeshAsset = "", Size = "small", Footprint = [[2, 2]], Floors = 1, Condition = 0.3f, Placement = new PlacementDto(0, 0, 2, 2) },
        };
        var mapResult = new TownMapResult
        {
            Layout = new LayoutDto { Width = 16, Height = 16, Surface = [[0]] },
            Buildings = buildings, Props = [],
            Zones = [new ZoneDto { Id = "z_0", Name = "Main", Biome = 0, RadiationLevel = 0, EnemyDifficultyTier = 1, IsFastTravelTarget = true, ChunkStartX = 0, ChunkStartY = 0, ChunkEndX = 0, ChunkEndY = 0 }],
        };

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
