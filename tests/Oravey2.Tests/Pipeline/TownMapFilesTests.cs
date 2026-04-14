using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class TownMapFilesTests
{
    private static TownMapResult CreateTestResult()
    {
        var layout = new TownLayout
        {
            Width = 16, Height = 16, Surface = [
                [0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1],
                [1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0, 1, 0],
            ],
        };
        var buildings = new List<PlacedBuilding>
        {
            new() { Id = "b_0", Name = "Fort Test", MeshAsset = "meshes/fort_test.glb", SizeCategory = "large",
                Footprint = [[7, 7], [7, 8], [8, 7], [8, 8]],
                Floors = 3, Condition = 0.6f, Placement = new TilePlacement(0, 0, 7, 7) },
            new() { Id = "b_1", Name = "Shop", MeshAsset = "meshes/shop.glb", SizeCategory = "small",
                Footprint = [[3, 5]], Floors = 1, Condition = 0.8f, Placement = new TilePlacement(0, 0, 3, 5) },
        };
        var props = new List<PlacedProp>
        {
            new() { Id = "p_0", MeshAsset = "meshes/barrel.glb", Placement = new TilePlacement(0, 0, 1, 1), Rotation = 45f, Scale = 1.0f, BlocksWalkability = false },
        };
        var zones = new List<TownZone>
        {
            new() { Id = "zone_main", Name = "TestTown", Biome = 0, RadiationLevel = 0f, EnemyDifficultyTier = 2, IsFastTravelTarget = true, ChunkStartX = 0, ChunkStartY = 0, ChunkEndX = 0, ChunkEndY = 0 },
        };
        return new TownMapResult { Layout = layout, Buildings = buildings, Props = props, Zones = zones };
    }

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var result = CreateTestResult();
        var dir = Path.Combine(Path.GetTempPath(), $"townmap_test_{Guid.NewGuid():N}");

        try
        {
            TownMapFiles.Save(result, dir);

            Assert.True(File.Exists(Path.Combine(dir, "layout.json")));
            Assert.True(File.Exists(Path.Combine(dir, "buildings.json")));
            Assert.True(File.Exists(Path.Combine(dir, "props.json")));
            Assert.True(File.Exists(Path.Combine(dir, "zones.json")));

            var loaded = TownMapFiles.Load(dir);

            Assert.Equal(result.Layout.Width, loaded.Layout.Width);
            Assert.Equal(result.Layout.Height, loaded.Layout.Height);
            Assert.Equal(result.Buildings.Count, loaded.Buildings.Count);
            Assert.Equal(result.Props.Count, loaded.Props.Count);
            Assert.Equal(result.Zones.Count, loaded.Zones.Count);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var result = CreateTestResult();
        var dir = Path.Combine(Path.GetTempPath(), $"townmap_test_{Guid.NewGuid():N}", "nested");

        try
        {
            TownMapFiles.Save(result, dir);
            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            var parent = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void SaveLoad_BuildingData_Preserved()
    {
        var result = CreateTestResult();
        var dir = Path.Combine(Path.GetTempPath(), $"townmap_test_{Guid.NewGuid():N}");

        try
        {
            TownMapFiles.Save(result, dir);
            var loaded = TownMapFiles.Load(dir);

            var original = result.Buildings[0];
            var loaded0 = loaded.Buildings[0];
            Assert.Equal(original.Id, loaded0.Id);
            Assert.Equal(original.Name, loaded0.Name);
            Assert.Equal(original.MeshAsset, loaded0.MeshAsset);
            Assert.Equal(original.SizeCategory, loaded0.SizeCategory);
            Assert.Equal(original.Floors, loaded0.Floors);
            Assert.Equal(original.Placement.ChunkX, loaded0.Placement.ChunkX);
            Assert.Equal(original.Placement.LocalTileX, loaded0.Placement.LocalTileX);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveLoad_PropData_Preserved()
    {
        var result = CreateTestResult();
        var dir = Path.Combine(Path.GetTempPath(), $"townmap_test_{Guid.NewGuid():N}");

        try
        {
            TownMapFiles.Save(result, dir);
            var loaded = TownMapFiles.Load(dir);

            var original = result.Props[0];
            var loaded0 = loaded.Props[0];
            Assert.Equal(original.Id, loaded0.Id);
            Assert.Equal(original.MeshAsset, loaded0.MeshAsset);
            Assert.Equal(original.Rotation, loaded0.Rotation);
            Assert.Equal(original.Scale, loaded0.Scale);
            Assert.Equal(original.BlocksWalkability, loaded0.BlocksWalkability);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveLoad_ZoneData_Preserved()
    {
        var result = CreateTestResult();
        var dir = Path.Combine(Path.GetTempPath(), $"townmap_test_{Guid.NewGuid():N}");

        try
        {
            TownMapFiles.Save(result, dir);
            var loaded = TownMapFiles.Load(dir);

            var original = result.Zones[0];
            var loaded0 = loaded.Zones[0];
            Assert.Equal(original.Id, loaded0.Id);
            Assert.Equal(original.Name, loaded0.Name);
            Assert.Equal(original.Biome, loaded0.Biome);
            Assert.Equal(original.IsFastTravelTarget, loaded0.IsFastTravelTarget);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_ProducesValidJson()
    {
        var result = CreateTestResult();
        var dir = Path.Combine(Path.GetTempPath(), $"townmap_test_{Guid.NewGuid():N}");

        try
        {
            TownMapFiles.Save(result, dir);

            var layoutJson = File.ReadAllText(Path.Combine(dir, "layout.json"));
            Assert.Contains("\"width\"", layoutJson);
            Assert.Contains("\"height\"", layoutJson);
            Assert.Contains("\"surface\"", layoutJson);

            var buildingsJson = File.ReadAllText(Path.Combine(dir, "buildings.json"));
            Assert.Contains("\"meshAsset\"", buildingsJson);
            Assert.Contains("\"placement\"", buildingsJson);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
