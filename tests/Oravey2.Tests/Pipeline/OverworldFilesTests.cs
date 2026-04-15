using System.Numerics;
using Oravey2.Contracts.ContentPack;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Pipeline;

public class OverworldFilesTests
{
    private static OverworldResult MakeTestResult()
    {
        var townRefs = new List<TownRefDto>
        {
            new TownRefDto { GameName = "haven", RealName = "Island Haven", GameX = 0.5f, GameY = 0.5f, Description = "A safe port town", Size = "Town", Inhabitants = 5000, Destruction = "Pristine" },
            new TownRefDto { GameName = "outpost", RealName = "Fort Outpost", GameX = 1.5f, GameY = 0.5f, Description = "A military outpost", Size = "Village", Inhabitants = 1000, Destruction = "Moderate" },
        };

        var world = new WorldDto
        {
            Name = "TestRegion", Description = "Test overworld", Source = "test-source",
            ChunksWide = 2, ChunksHigh = 1, TileSize = 1,
            PlayerStart = new PlacementDto(0, 0, 8, 8),
            Towns = townRefs,
        };

        var roads = new List<OverworldRoad>
        {
            new() { Id = "road_0", RoadClass = "Primary",
                Nodes = [new Vector2(0.5f, 0.5f), new Vector2(1.5f, 0.5f)],
                FromTown = "haven", ToTown = "outpost" },
        };

        var water = new List<OverworldWater>
        {
            new() { Id = "water_0", WaterType = "Sea",
                Geometry = [new Vector2(0, 0), new Vector2(2, 0), new Vector2(2, 1)] },
        };

        return new OverworldResult { World = world, Roads = roads, Water = water };
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
            Assert.Equal("A safe port town", town.Description);
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

            Assert.Equal(0, loaded.World.PlayerStart!.ChunkX);
            Assert.Equal(8, loaded.World.PlayerStart!.LocalTileX);
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
