using System.Numerics;
using Oravey2.Core.World;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.Tests.Pipeline;

public class RegionTemplateSerializerTests : IDisposable
{
    private readonly string _tempDir;

    public RegionTemplateSerializerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"oravey2_ser_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private string TempPath(string name = "test.bin") => Path.Combine(_tempDir, name);

    private static RegionTemplate MakeTemplate() => new()
    {
        Name = "test-region",
        ElevationGrid = new float[,] { { 1.5f, 2.0f, 3.1f }, { 4.0f, 5.5f, 6.2f } },
        GridOriginLat = 52.5,
        GridOriginLon = 4.8,
        GridCellSizeMetres = 30.0,
        Towns =
        [
            new("Amsterdam", 52.37, 4.90, 900000, new Vector2(100, 200), TownCategory.City),
            new("Volendam", 52.49, 5.07, 22000, new Vector2(300, 400), TownCategory.Village,
                [new Vector2(1, 2), new Vector2(3, 4), new Vector2(5, 6)]),
        ],
        Roads =
        [
            new(LinearFeatureType.Motorway, [new Vector2(0, 0), new Vector2(10, 20)]),
            new(LinearFeatureType.Tertiary, [new Vector2(5, 5), new Vector2(15, 25), new Vector2(30, 40)]),
        ],
        WaterBodies =
        [
            new(WaterType.Lake, [new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1)]),
            new(WaterType.River, [new Vector2(10, 10), new Vector2(20, 20)]),
        ],
        Railways =
        [
            new([new Vector2(0, 0), new Vector2(50, 50)]),
        ],
        LandUseZones =
        [
            new(LandUseType.Forest, [new Vector2(0, 0), new Vector2(10, 0), new Vector2(10, 10)]),
        ],
    };

    // --- Round-trip ---

    [Fact]
    public async Task Serializer_RoundTrip_PreservesName()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal("test-region", loaded.Name);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesGridMetadata()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(52.5, loaded.GridOriginLat);
        Assert.Equal(4.8, loaded.GridOriginLon);
        Assert.Equal(30.0, loaded.GridCellSizeMetres);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesElevationGrid()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.ElevationGrid.GetLength(0));
        Assert.Equal(3, loaded.ElevationGrid.GetLength(1));
        Assert.Equal(1.5f, loaded.ElevationGrid[0, 0]);
        Assert.Equal(6.2f, loaded.ElevationGrid[1, 2]);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesTowns()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Towns.Count);

        var ams = loaded.Towns[0];
        Assert.Equal("Amsterdam", ams.Name);
        Assert.Equal(52.37, ams.Latitude);
        Assert.Equal(4.90, ams.Longitude);
        Assert.Equal(900000, ams.Population);
        Assert.Equal(100f, ams.GamePosition.X);
        Assert.Equal(200f, ams.GamePosition.Y);
        Assert.Equal(TownCategory.City, ams.Category);
        Assert.Null(ams.BoundaryPolygon);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesBoundaryPolygon()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        var vol = loaded.Towns[1];
        Assert.Equal("Volendam", vol.Name);
        Assert.NotNull(vol.BoundaryPolygon);
        Assert.Equal(3, vol.BoundaryPolygon.Length);
        Assert.Equal(new Vector2(3, 4), vol.BoundaryPolygon[1]);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesRoads()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.Roads.Count);
        Assert.Equal(LinearFeatureType.Motorway, loaded.Roads[0].RoadClass);
        Assert.Equal(2, loaded.Roads[0].Nodes.Length);
        Assert.Equal(LinearFeatureType.Tertiary, loaded.Roads[1].RoadClass);
        Assert.Equal(3, loaded.Roads[1].Nodes.Length);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesWater()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Equal(2, loaded.WaterBodies.Count);
        Assert.Equal(WaterType.Lake, loaded.WaterBodies[0].Type);
        Assert.Equal(4, loaded.WaterBodies[0].Geometry.Length);
        Assert.Equal(WaterType.River, loaded.WaterBodies[1].Type);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesRailways()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Single(loaded.Railways);
        Assert.Equal(2, loaded.Railways[0].Nodes.Length);
        Assert.Equal(new Vector2(50, 50), loaded.Railways[0].Nodes[1]);
    }

    [Fact]
    public async Task Serializer_RoundTrip_PreservesLandUse()
    {
        var original = MakeTemplate();
        var path = TempPath();

        await RegionTemplateSerializer.SaveAsync(original, path);
        var loaded = await RegionTemplateSerializer.LoadAsync(path);

        Assert.NotNull(loaded);
        Assert.Single(loaded.LandUseZones);
        Assert.Equal(LandUseType.Forest, loaded.LandUseZones[0].Type);
        Assert.Equal(3, loaded.LandUseZones[0].Polygon.Length);
    }

    // --- Corrupt / missing ---

    [Fact]
    public async Task Serializer_MissingFile_ReturnsNull()
    {
        var result = await RegionTemplateSerializer.LoadAsync(TempPath("nonexistent.bin"));
        Assert.Null(result);
    }

    [Fact]
    public async Task Serializer_TruncatedFile_ReturnsNull()
    {
        var path = TempPath("truncated.bin");
        await File.WriteAllBytesAsync(path, [0x4F, 0x52, 0x52, 0x54, 0x01, 0x00]); // magic + partial version

        var result = await RegionTemplateSerializer.LoadAsync(path);
        Assert.Null(result);
    }

    [Fact]
    public async Task Serializer_WrongMagic_ReturnsNull()
    {
        var path = TempPath("badmagic.bin");
        await File.WriteAllBytesAsync(path, [0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00]);

        var result = await RegionTemplateSerializer.LoadAsync(path);
        Assert.Null(result);
    }

    [Fact]
    public async Task Serializer_EmptyFile_ReturnsNull()
    {
        var path = TempPath("empty.bin");
        await File.WriteAllBytesAsync(path, []);

        var result = await RegionTemplateSerializer.LoadAsync(path);
        Assert.Null(result);
    }
}
