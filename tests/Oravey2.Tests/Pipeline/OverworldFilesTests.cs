using System.Numerics;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class OverworldFilesTests
{
    private static OverworldResult MakeTestResult()
    {
        var townRefs = new List<OverworldTownRef>
        {
            new("haven", "Island Haven", 0.5f, 0.5f, "safe_haven", 1),
            new("outpost", "Fort Outpost", 1.5f, 0.5f, "militia_base", 3),
        };

        var world = new OverworldInfo(
            "TestRegion", "Test overworld", "test-source",
            ChunksWide: 2, ChunksHigh: 1, TileSize: 1,
            new TilePlacement(0, 0, 8, 8),
            townRefs);

        var roads = new List<OverworldRoad>
        {
            new("road_0", "Primary",
                [new Vector2(0.5f, 0.5f), new Vector2(1.5f, 0.5f)],
                "haven", "outpost"),
        };

        var water = new List<OverworldWater>
        {
            new("water_0", "Sea",
                [new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 1)]),
        };

        return new OverworldResult(world, roads, water);
    }

    [Fact]
    public void SaveLoad_RoundTrips()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ow_test_{Guid.NewGuid():N}");
        try
        {
            var original = MakeTestResult();
            OverworldFiles.Save(original, dir);

            Assert.True(File.Exists(Path.Combine(dir, "world.json")));
            Assert.True(File.Exists(Path.Combine(dir, "roads.json")));
            Assert.True(File.Exists(Path.Combine(dir, "water.json")));

            var loaded = OverworldFiles.Load(dir);

            Assert.Equal(original.World.Name, loaded.World.Name);
            Assert.Equal(original.World.ChunksWide, loaded.World.ChunksWide);
            Assert.Equal(original.World.ChunksHigh, loaded.World.ChunksHigh);
            Assert.Equal(original.World.Towns.Count, loaded.World.Towns.Count);
            Assert.Equal(original.Roads.Count, loaded.Roads.Count);
            Assert.Equal(original.Water.Count, loaded.Water.Count);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_CreatesDirectory()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ow_test_{Guid.NewGuid():N}", "nested");
        try
        {
            OverworldFiles.Save(MakeTestResult(), dir);
            Assert.True(Directory.Exists(dir));
        }
        finally
        {
            var parent = Path.GetDirectoryName(dir)!;
            if (Directory.Exists(parent)) Directory.Delete(parent, true);
        }
    }

    [Fact]
    public void SaveLoad_TownRefs_Preserved()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ow_test_{Guid.NewGuid():N}");
        try
        {
            OverworldFiles.Save(MakeTestResult(), dir);
            var loaded = OverworldFiles.Load(dir);

            var town = loaded.World.Towns.First(t => t.GameName == "haven");
            Assert.Equal("Island Haven", town.RealName);
            Assert.Equal(0.5f, town.GameX, 0.01f);
            Assert.Equal("safe_haven", town.Role);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveLoad_Roads_Preserved()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ow_test_{Guid.NewGuid():N}");
        try
        {
            OverworldFiles.Save(MakeTestResult(), dir);
            var loaded = OverworldFiles.Load(dir);

            Assert.Single(loaded.Roads);
            Assert.Equal("Primary", loaded.Roads[0].RoadClass);
            Assert.Equal("haven", loaded.Roads[0].FromTown);
            Assert.Equal("outpost", loaded.Roads[0].ToTown);
            Assert.Equal(2, loaded.Roads[0].Nodes.Length);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveLoad_Water_Preserved()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ow_test_{Guid.NewGuid():N}");
        try
        {
            OverworldFiles.Save(MakeTestResult(), dir);
            var loaded = OverworldFiles.Load(dir);

            Assert.Single(loaded.Water);
            Assert.Equal("Sea", loaded.Water[0].WaterType);
            Assert.Equal(3, loaded.Water[0].Geometry.Length);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void SaveLoad_PlayerStart_Preserved()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ow_test_{Guid.NewGuid():N}");
        try
        {
            OverworldFiles.Save(MakeTestResult(), dir);
            var loaded = OverworldFiles.Load(dir);

            Assert.Equal(0, loaded.World.PlayerStart.ChunkX);
            Assert.Equal(8, loaded.World.PlayerStart.LocalTileX);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }

    [Fact]
    public void Save_ProducesValidJson()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"ow_test_{Guid.NewGuid():N}");
        try
        {
            OverworldFiles.Save(MakeTestResult(), dir);

            var worldJson = File.ReadAllText(Path.Combine(dir, "world.json"));
            Assert.Contains("\"chunksWide\"", worldJson);
            Assert.Contains("\"playerStart\"", worldJson);
            Assert.Contains("\"towns\"", worldJson);
        }
        finally
        {
            if (Directory.Exists(dir)) Directory.Delete(dir, true);
        }
    }
}
