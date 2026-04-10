using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class AssemblyStepViewModelTests
{
    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"asmvm_test_{Guid.NewGuid():N}");

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    private static void SetupValidPack(string root)
    {
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "manifest.json"),
            """{"id":"test","name":"Test","version":"0.1.0","parent":"test.parent"}""");

        var dataDir = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "curated-towns.json"),
            """{"towns": [{"gameName": "haven", "realName": "Island Haven"}]}""");

        var townDir = Path.Combine(root, "towns", "haven");
        Directory.CreateDirectory(townDir);
        File.WriteAllText(Path.Combine(townDir, "design.json"), "{}");
        File.WriteAllText(Path.Combine(townDir, "layout.json"), "{}");
        File.WriteAllText(Path.Combine(townDir, "buildings.json"), "[]");
        File.WriteAllText(Path.Combine(townDir, "props.json"), "[]");
        File.WriteAllText(Path.Combine(townDir, "zones.json"), "[]");

        File.WriteAllText(Path.Combine(root, "catalog.json"),
            """{"building":[],"prop":[],"surface":[],"terrain_mesh":[]}""");
    }

    [Fact]
    public void Defaults_StatusText_IsPrompt()
    {
        var vm = new AssemblyStepViewModel();
        Assert.Contains("Validate", vm.StatusText);
    }

    [Fact]
    public void Defaults_NotValidated()
    {
        var vm = new AssemblyStepViewModel();
        Assert.False(vm.IsValidated);
        Assert.False(vm.ValidationPassed);
    }

    [Fact]
    public void Load_SetsScenarioDefaults()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Noord-Holland" });

            Assert.Equal("noord-holland", vm.ScenarioId);
            Assert.Contains("Noord-Holland", vm.ScenarioName);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void RunValidation_ValidPack_Passes()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Test" });
            vm.RunValidation();

            Assert.True(vm.IsValidated);
            Assert.True(vm.ValidationPassed);
            Assert.NotEmpty(vm.ValidationItems);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void RunValidation_InvalidPack_FailsWithErrors()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            // No manifest = validation error
            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Test" });
            vm.RunValidation();

            Assert.True(vm.IsValidated);
            Assert.False(vm.ValidationPassed);
            Assert.Contains(vm.ValidationItems, i =>
                i.Severity == ValidationSeverity.Error);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void RunGenerateScenario_CreatesFile()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Test" });
            vm.ScenarioId = "test-scenario";
            vm.RunGenerateScenario();

            Assert.True(File.Exists(Path.Combine(root, "scenarios", "test-scenario.json")));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void RunRebuildCatalog_UpdatesCatalog()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var meshDir = Path.Combine(root, "assets", "meshes");
            Directory.CreateDirectory(meshDir);
            File.WriteAllBytes(Path.Combine(meshDir, "test.glb"), [0x01]);

            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Test" });
            vm.RunRebuildCatalog();

            var json = File.ReadAllText(Path.Combine(root, "catalog.json"));
            Assert.Contains("test", json);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void RunUpdateManifest_WritesManifest()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Test" });
            vm.RunUpdateManifest();

            Assert.True(File.Exists(Path.Combine(root, "manifest.json")));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void GetTownGameNames_ReturnsDesignedTowns()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Test" });

            var names = vm.GetTownGameNames();
            Assert.Single(names);
            Assert.Equal("haven", names[0]);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void CompleteCommand_NotExecutable_BeforeValidation()
    {
        var vm = new AssemblyStepViewModel();
        Assert.False(vm.CompleteCommand.CanExecute(null));
    }

    [Fact]
    public void CompleteCommand_Executable_AfterPassingValidation()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var vm = new AssemblyStepViewModel();
            vm.Load(new PipelineState { ContentPackPath = root, RegionName = "Test" });
            vm.RunValidation();

            Assert.True(vm.CompleteCommand.CanExecute(null));
        }
        finally { Cleanup(root); }
    }
}
