using Oravey2.MapGen.Pipeline;

namespace Oravey2.Tests.Pipeline;

public class ContentPackAssemblerTests
{
    private static string CreateTempRoot() =>
        Path.Combine(Path.GetTempPath(), $"asm_test_{Guid.NewGuid():N}");

    private static void Cleanup(string root)
    {
        if (Directory.Exists(root))
            Directory.Delete(root, true);
    }

    // --- GenerateScenario ---

    [Fact]
    public void GenerateScenario_CreatesScenarioFile()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            var assembler = new ContentPackAssembler();
            var settings = new ScenarioSettings
            {
                Id = "noord-holland",
                Name = "Noord-Holland Wastes",
                Description = "Survive the flooded polders.",
                Difficulty = 3,
                Tags = ["exploration", "coastal"],
                PlayerStart = new PlayerStartInfo { Town = "havenburg", TileX = 5, TileY = 5 },
            };

            assembler.GenerateScenario(root, ["havenburg", "marsdiep"], settings);

            var path = Path.Combine(root, "scenarios", "noord-holland.json");
            Assert.True(File.Exists(path));

            var json = File.ReadAllText(path);
            Assert.Contains("\"id\": \"noord-holland\"", json);
            Assert.Contains("\"havenburg\"", json);
            Assert.Contains("\"marsdiep\"", json);
            Assert.Contains("\"tileX\": 5", json);
        }
        finally { Cleanup(root); }
    }

    // --- RebuildCatalog ---

    [Fact]
    public void RebuildCatalog_CatalogsAllGlbFiles()
    {
        var root = CreateTempRoot();
        try
        {
            var meshDir = Path.Combine(root, "assets", "meshes");
            Directory.CreateDirectory(meshDir);
            File.WriteAllBytes(Path.Combine(meshDir, "building-a.glb"), [0x01]);
            File.WriteAllBytes(Path.Combine(meshDir, "building-b.glb"), [0x02]);

            var assembler = new ContentPackAssembler();
            assembler.RebuildCatalog(root);

            var catalogPath = Path.Combine(root, "catalog.json");
            Assert.True(File.Exists(catalogPath));

            var json = File.ReadAllText(catalogPath);
            Assert.Contains("building-a", json);
            Assert.Contains("building-b", json);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void RebuildCatalog_EmptyMeshDir_ProducesEmptyCatalog()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(root);

            var assembler = new ContentPackAssembler();
            assembler.RebuildCatalog(root);

            var json = File.ReadAllText(Path.Combine(root, "catalog.json"));
            Assert.Contains("\"building\": []", json);
        }
        finally { Cleanup(root); }
    }

    // --- UpdateManifest ---

    [Fact]
    public void UpdateManifest_CreatesNewManifest()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            var assembler = new ContentPackAssembler();
            assembler.UpdateManifest(root, new ManifestUpdate
            {
                Name = "Test Region",
                Version = "1.0.0",
                Description = "A test.",
                Author = "Tester",
            });

            var path = Path.Combine(root, "manifest.json");
            Assert.True(File.Exists(path));

            var json = File.ReadAllText(path);
            Assert.Contains("\"name\": \"Test Region\"", json);
            Assert.Contains("\"version\": \"1.0.0\"", json);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void UpdateManifest_UpdatesExistingManifest()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            File.WriteAllText(Path.Combine(root, "manifest.json"),
                """{"id":"test","name":"Old","version":"0.1.0","description":"","author":"","engineVersion":">=0.1.0","parent":"","tags":[],"defaultScenario":""}""");

            var assembler = new ContentPackAssembler();
            assembler.UpdateManifest(root, new ManifestUpdate { Version = "2.0.0" });

            var json = File.ReadAllText(Path.Combine(root, "manifest.json"));
            Assert.Contains("\"version\": \"2.0.0\"", json);
            Assert.Contains("\"name\": \"Old\"", json); // unchanged
        }
        finally { Cleanup(root); }
    }

    // --- Validate ---

    [Fact]
    public void Validate_MissingManifest_ReturnsError()
    {
        var root = CreateTempRoot();
        try
        {
            Directory.CreateDirectory(root);
            var assembler = new ContentPackAssembler();
            var result = assembler.Validate(root);

            Assert.False(result.Passed);
            Assert.True(result.ErrorCount > 0);
            Assert.Contains(result.Items, i =>
                i.Check == "Manifest" && i.Severity == ValidationSeverity.Error);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void Validate_ValidPack_Passes()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            var assembler = new ContentPackAssembler();
            var result = assembler.Validate(root);

            Assert.True(result.Passed);
            Assert.Equal(0, result.ErrorCount);
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void Validate_MissingTownDesign_ReportsError()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            // Add a curated town entry for a town without a design
            var curatedPath = Path.Combine(root, "data", "curated-towns.json");
            var json = File.ReadAllText(curatedPath);
            json = json.Replace("\"towns\": [", """
                "towns": [
                    {"gameName": "ghost-town", "realName": "Ghost Town", "role": "camp"},
                """);
            File.WriteAllText(curatedPath, json);

            var assembler = new ContentPackAssembler();
            var result = assembler.Validate(root);

            Assert.Contains(result.Items, i =>
                i.Check == "Town Design" && i.Severity == ValidationSeverity.Error
                && i.Detail.Contains("ghost-town"));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void Validate_MissingMapFiles_ReportsError()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            // Remove layout.json from a designed town
            File.Delete(Path.Combine(root, "towns", "haven", "layout.json"));

            var assembler = new ContentPackAssembler();
            var result = assembler.Validate(root);

            Assert.Contains(result.Items, i =>
                i.Check == "Town Maps" && i.Severity == ValidationSeverity.Error
                && i.Detail.Contains("layout.json"));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void Validate_BrokenMeshRef_ReportsError()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            // Add a building with a mesh reference that doesn't exist
            var buildingsPath = Path.Combine(root, "towns", "haven", "buildings.json");
            File.WriteAllText(buildingsPath, """
                [{"id": "b_0", "name": "Fort", "meshAsset": "assets/meshes/nonexistent.glb"}]
                """);

            var assembler = new ContentPackAssembler();
            var result = assembler.Validate(root);

            Assert.Contains(result.Items, i =>
                i.Check == "Mesh Refs" && i.Severity == ValidationSeverity.Error
                && i.Detail.Contains("nonexistent.glb"));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void Validate_OrphanMesh_ReportsWarning()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            // Add a .glb without .meta.json
            var meshDir = Path.Combine(root, "assets", "meshes");
            Directory.CreateDirectory(meshDir);
            File.WriteAllBytes(Path.Combine(meshDir, "orphan.glb"), [0x01]);

            var assembler = new ContentPackAssembler();
            var result = assembler.Validate(root);

            Assert.Contains(result.Items, i =>
                i.Check == "Orphan Meshes" && i.Severity == ValidationSeverity.Warning
                && i.Detail.Contains("orphan.glb"));
        }
        finally { Cleanup(root); }
    }

    [Fact]
    public void Validate_ScenarioRefsNonExistentTown_ReportsError()
    {
        var root = CreateTempRoot();
        try
        {
            SetupValidPack(root);
            // Create a scenario referencing a non-existent town
            var scenDir = Path.Combine(root, "scenarios");
            Directory.CreateDirectory(scenDir);
            File.WriteAllText(Path.Combine(scenDir, "test.json"),
                """{"id":"test","towns":["nonexistent"]}""");

            var assembler = new ContentPackAssembler();
            var result = assembler.Validate(root);

            Assert.Contains(result.Items, i =>
                i.Check == "Scenarios" && i.Severity == ValidationSeverity.Error
                && i.Detail.Contains("nonexistent"));
        }
        finally { Cleanup(root); }
    }

    // --- Helpers ---

    private static void SetupValidPack(string root)
    {
        Directory.CreateDirectory(root);

        // manifest.json
        File.WriteAllText(Path.Combine(root, "manifest.json"),
            """{"id":"test","name":"Test","version":"0.1.0","parent":"test.parent"}""");

        // data/curated-towns.json
        var dataDir = Path.Combine(root, "data");
        Directory.CreateDirectory(dataDir);
        File.WriteAllText(Path.Combine(dataDir, "curated-towns.json"),
            """{"towns": [{"gameName": "haven", "realName": "Island Haven"}]}""");

        // towns/haven with full map files
        var townDir = Path.Combine(root, "towns", "haven");
        Directory.CreateDirectory(townDir);
        File.WriteAllText(Path.Combine(townDir, "design.json"), "{}");
        File.WriteAllText(Path.Combine(townDir, "layout.json"), "{}");
        File.WriteAllText(Path.Combine(townDir, "buildings.json"), "[]");
        File.WriteAllText(Path.Combine(townDir, "props.json"), "[]");
        File.WriteAllText(Path.Combine(townDir, "zones.json"), "[]");

        // catalog.json
        File.WriteAllText(Path.Combine(root, "catalog.json"),
            """{"building":[],"prop":[],"surface":[],"terrain_mesh":[]}""");
    }
}
