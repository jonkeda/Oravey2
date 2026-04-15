using Oravey2.Contracts.ContentPack;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class AssetFilesTests
{
    [Fact]
    public void SaveLoadMeta_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af_test_{Guid.NewGuid():N}");
        try
        {
            Directory.CreateDirectory(dir);
            var metaPath = Path.Combine(dir, "test.meta.json");

            var original = new AssetMeta
            {
                AssetId = "haven-the-beacon",
                MeshyTaskId = "task_abc123",
                Prompt = "A towering lighthouse on a cliff",
                GeneratedAt = new DateTime(2026, 4, 9, 15, 0, 0, DateTimeKind.Utc),
                Status = "accepted",
                SourceType = "text-to-3d",
                SizeCategory = "large",
            };

            AssetFiles.SaveMeta(original, metaPath);
            Assert.True(File.Exists(metaPath));

            var loaded = AssetFiles.LoadMeta(metaPath);
            Assert.Equal("haven-the-beacon", loaded.AssetId);
            Assert.Equal("task_abc123", loaded.MeshyTaskId);
            Assert.Equal("A towering lighthouse on a cliff", loaded.Prompt);
            Assert.Equal("accepted", loaded.Status);
            Assert.Equal("large", loaded.SizeCategory);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveMeta_CreatesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af_test_{Guid.NewGuid():N}", "nested");
        try
        {
            var metaPath = Path.Combine(dir, "test.meta.json");
            AssetFiles.SaveMeta(new AssetMeta { AssetId = "test" }, metaPath);
            Assert.True(File.Exists(metaPath));
        }
        finally
        {
            var parent = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void UpdateBuildingMeshReference_UpdatesMatchingBuilding()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af_test_{Guid.NewGuid():N}");
        try
        {
            // Write a buildings.json with a placeholder
            var townDir = Path.Combine(dir, "testtown");
            Directory.CreateDirectory(townDir);

            var buildings = new List<BuildingDto>
            {
                new BuildingDto { Id = "b_0", Name = "The Beacon", MeshAsset = "meshes/the_beacon.glb", Size = "large",
                    Footprint = [[0, 0]], Floors = 2, Condition = 0.5f, Placement = new PlacementDto(0, 0, 0, 0) },
                new BuildingDto { Id = "b_1", Name = "Market", MeshAsset = "meshes/market.glb", Size = "medium",
                    Footprint = [[1, 1]], Floors = 1, Condition = 0.8f, Placement = new PlacementDto(0, 0, 1, 1) },
            };

            var result = new TownMapResult
            {
                Layout = new LayoutDto { Width = 16, Height = 16, Surface = [] },
                Buildings = buildings, Props = [], Zones = [],
            };
            TownMapFiles.Save(result, townDir);

            // Update The Beacon's mesh reference
            AssetFiles.UpdateBuildingMeshReference(townDir, "The Beacon", "meshes/haven-the-beacon.glb");

            // Verify
            var loaded = TownMapFiles.Load(townDir);
            Assert.Equal("meshes/haven-the-beacon.glb", loaded.Buildings[0].MeshAsset);
            Assert.Equal("meshes/market.glb", loaded.Buildings[1].MeshAsset); // unchanged
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void UpdateBuildingMeshReference_NoMatch_DoesNothing()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af_test_{Guid.NewGuid():N}");
        try
        {
            var townDir = Path.Combine(dir, "testtown");
            Directory.CreateDirectory(townDir);

            var buildings = new List<BuildingDto>
            {
                new BuildingDto { Id = "b_0", Name = "Market", MeshAsset = "meshes/market.glb", Size = "medium",
                    Footprint = [[0, 0]], Floors = 1, Condition = 0.8f, Placement = new PlacementDto(0, 0, 0, 0) },
            };
            var result = new TownMapResult
            {
                Layout = new LayoutDto { Width = 16, Height = 16, Surface = [] },
                Buildings = buildings, Props = [], Zones = [],
            };
            TownMapFiles.Save(result, townDir);

            AssetFiles.UpdateBuildingMeshReference(townDir, "NonExistent", "meshes/test.glb");

            var loaded = TownMapFiles.Load(townDir);
            Assert.Equal("meshes/market.glb", loaded.Buildings[0].MeshAsset);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void UpdateBuildingMeshReference_MissingFile_NoError()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"af_test_{Guid.NewGuid():N}");
        AssetFiles.UpdateBuildingMeshReference(dir, "Missing", "meshes/test.glb");
        // Should not throw
    }
}
