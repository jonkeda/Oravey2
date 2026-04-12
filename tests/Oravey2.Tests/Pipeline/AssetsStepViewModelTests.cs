using Oravey2.MapGen.Assets;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class AssetsStepViewModelTests
{
    private static string SetupContentPack(
        params (string gameName, TownDesign design, TownMapResult? mapResult)[] towns)
    {
        var root = Path.Combine(Path.GetTempPath(), $"asvm_test_{Guid.NewGuid():N}");
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
        [new LandmarkBuilding("The Beacon", "A tall lighthouse on a cliff", "large", "", "", "")],
        [new KeyLocation("Market Hall", "shop", "A bustling market building", "medium", "", "", "")],
        "organic",
        []);

    private static TownMapResult MakeMapResult() => new(
        new TownLayout(16, 16, [[0]]),
        [
            new PlacedBuilding("b_0", "The Beacon", "", "large", [[0, 0]], 2, 0.5f, new TilePlacement(0, 0, 0, 0)),
            new PlacedBuilding("b_1", "Market Hall", "", "medium", [[1, 1]], 1, 0.8f, new TilePlacement(0, 0, 1, 1)),
        ],
        [new PlacedProp("p_0", "", new TilePlacement(0, 0, 3, 3), 0, 1, false)],
        [new TownZone("z_0", "Main", 0, 0, 1, true, 0, 0, 0, 0)]);

    [Fact]
    public void Defaults_StatusText_IsPrompt()
    {
        var vm = new AssetsStepViewModel();
        Assert.Contains("Load", vm.StatusText);
    }

    [Fact]
    public void Defaults_NoSelection()
    {
        var vm = new AssetsStepViewModel();
        Assert.Null(vm.SelectedBuilding);
        Assert.False(vm.HasSelection);
    }

    [Fact]
    public void Load_PopulatesTownsAndBuildings()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });

            Assert.Single(vm.Towns);
            Assert.Equal("Island Haven", vm.Towns[0].TownName);
            Assert.Equal(2, vm.Towns[0].Buildings.Count);
            Assert.Contains(vm.Towns[0].Buildings, b => b.Name == "The Beacon");
            Assert.Contains(vm.Towns[0].Buildings, b => b.Name == "Market Hall");
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Load_DetectsExistingRealMesh()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var meshDir = Path.Combine(root, "assets", "meshes");
            Directory.CreateDirectory(meshDir);
            // Place a .glb but we need to update buildings.json to point to it
            var townDir = Path.Combine(root, "towns", "haven");
            var mapResult = TownMapFiles.Load(townDir);
            var updated = mapResult with
            {
                Buildings = [
                    mapResult.Buildings[0] with { MeshAsset = "meshes/island-haven-the-beacon.glb" },
                    mapResult.Buildings[1],
                ],
            };
            TownMapFiles.Save(updated, townDir);
            File.WriteAllBytes(Path.Combine(meshDir, "island-haven-the-beacon.glb"), [0x01]);

            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });

            var beacon = vm.Towns[0].Buildings.First(b => b.Name == "The Beacon");
            Assert.Equal(MeshStatus.Ready, beacon.Status);
            Assert.Equal(1, vm.ReadyCount);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Load_BuildingRoles_AreCorrect()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });

            var beacon = vm.Towns[0].Buildings.First(b => b.Name == "The Beacon");
            var market = vm.Towns[0].Buildings.First(b => b.Name == "Market Hall");
            Assert.Equal("landmark", beacon.Role);
            Assert.Equal("key", market.Role);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FilterMode_All_ShowsAllTowns()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });
            vm.FilterMode = "All";

            Assert.Single(vm.FilteredTowns);
            Assert.Equal(vm.Towns[0].Buildings.Count, vm.FilteredTowns[0].Buildings.Count);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void FilterMode_None_FiltersCorrectly()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });
            vm.FilterMode = "None";

            Assert.All(vm.FilteredTowns.SelectMany(t => t.Buildings),
                b => Assert.Equal(MeshStatus.None, b.Status));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void SummaryText_ReflectsCounts()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });

            Assert.Contains("Towns: 1", vm.SummaryText);
            Assert.Contains("Buildings: 2", vm.SummaryText);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void TownAssetGroup_CompletionText_ShowsCounts()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });

            Assert.Equal("(0/2)", vm.Towns[0].CompletionText);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void RejectSelected_ResetsToNone()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });

            var item = vm.Towns[0].Buildings[0];
            item.Status = MeshStatus.Ready;
            item.HasResult = true;
            item.GlbDownloadUrl = "https://example.com/model.glb";
            vm.SelectedBuilding = item;

            vm.RejectSelected();

            Assert.Equal(MeshStatus.None, item.Status);
            Assert.False(item.HasResult);
            Assert.Null(item.GlbDownloadUrl);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void BuildingItem_StatusIcon_None()
    {
        var item = new BuildingItem { Status = MeshStatus.None };
        Assert.Equal("⚫", item.StatusIcon);
    }

    [Fact]
    public void BuildingItem_StatusIcon_Ready()
    {
        var item = new BuildingItem { Status = MeshStatus.Ready };
        Assert.Equal("🟢", item.StatusIcon);
    }

    [Fact]
    public void BuildingItem_StatusIcon_Failed()
    {
        var item = new BuildingItem { Status = MeshStatus.Failed };
        Assert.Equal("🔴", item.StatusIcon);
    }

    [Fact]
    public void BuildingItem_StatusIcon_Primitive()
    {
        var item = new BuildingItem { Status = MeshStatus.Primitive };
        Assert.Equal("🟡", item.StatusIcon);
    }

    [Fact]
    public void BuildingItem_RoleIcon_Landmark()
    {
        var item = new BuildingItem { Role = "landmark" };
        Assert.Equal("★", item.RoleIcon);
    }

    [Fact]
    public void BuildingItem_RoleIcon_Key()
    {
        var item = new BuildingItem { Role = "key" };
        Assert.Equal("●", item.RoleIcon);
    }

    [Fact]
    public void BuildingItem_RoleIcon_Generic()
    {
        var item = new BuildingItem { Role = "generic" };
        Assert.Equal("○", item.RoleIcon);
    }

    [Fact]
    public void BuildingItem_PromptSnippet_TruncatesLongDescription()
    {
        var item = new BuildingItem
        {
            VisualDescription = new string('A', 100),
        };
        Assert.Equal(63, item.PromptSnippet.Length); // 60 + "..."
        Assert.EndsWith("...", item.PromptSnippet);
    }

    [Fact]
    public void BuildingItem_PromptSnippet_ShortDescription()
    {
        var item = new BuildingItem { VisualDescription = "Short" };
        Assert.Equal("Short", item.PromptSnippet);
    }

    [Fact]
    public void BuildingItem_PromptSnippet_EmptyDescription()
    {
        var item = new BuildingItem { VisualDescription = "" };
        Assert.Equal("(no description)", item.PromptSnippet);
    }

    [Fact]
    public void FilterModes_HasFiveOptions()
    {
        var vm = new AssetsStepViewModel();
        Assert.Equal(5, vm.FilterModes.Count);
        Assert.Contains("All", vm.FilterModes);
        Assert.Contains("None", vm.FilterModes);
        Assert.Contains("Primitive", vm.FilterModes);
        Assert.Contains("Ready", vm.FilterModes);
        Assert.Contains("Failed", vm.FilterModes);
    }

    [Fact]
    public void GetState_ReturnsPipelineState()
    {
        var vm = new AssetsStepViewModel();
        var state = new PipelineState { ContentPackPath = Path.GetTempPath() };
        vm.Load(state);
        Assert.Same(state, vm.GetState());
    }

    [Fact]
    public void AssignDummyMeshes_UpdatesBuildingFiles()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });
            vm.AssignDummyMeshes();

            // Verify buildings were updated
            var townDir = Path.Combine(root, "towns", "haven");
            var loaded = TownMapFiles.Load(townDir);
            Assert.Equal(PrimitiveMeshWriter.PyramidPath, loaded.Buildings[0].MeshAsset); // landmark
            Assert.Equal(PrimitiveMeshWriter.CubePath, loaded.Buildings[1].MeshAsset);    // key location
            Assert.Equal(PrimitiveMeshWriter.SpherePath, loaded.Props[0].MeshAsset);      // prop

            // Verify primitives were created
            var primDir = Path.Combine(root, "assets", "meshes", "primitives");
            Assert.True(File.Exists(Path.Combine(primDir, "pyramid.glb")));
            Assert.True(File.Exists(Path.Combine(primDir, "cube.glb")));
            Assert.True(File.Exists(Path.Combine(primDir, "sphere.glb")));

            Assert.Contains("primitive meshes", vm.StatusText);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void AssignDummyMeshes_RefreshesStatusToPrimitive()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), MakeMapResult()));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });
            vm.AssignDummyMeshes();

            // After assign + reload, buildings should show Primitive status
            Assert.All(vm.Towns[0].Buildings,
                b => Assert.Equal(MeshStatus.Primitive, b.Status));
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void Load_DesignOnly_NoBuildingsJson_StillShowsBuildings()
    {
        var root = SetupContentPack(("haven", MakeDesign("Island Haven"), null));
        try
        {
            var vm = new AssetsStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root });

            Assert.Single(vm.Towns);
            Assert.Equal(2, vm.Towns[0].Buildings.Count);
            Assert.All(vm.Towns[0].Buildings, b =>
            {
                Assert.Equal(MeshStatus.None, b.Status);
                Assert.Equal("", b.BuildingId); // no placement data
            });
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
