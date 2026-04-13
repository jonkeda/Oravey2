using System.Numerics;
using Xunit;
using Oravey2.MapGen.Generation;

namespace Oravey2.Tests.Generation;

public class SpatialSpecPersistenceTests : IDisposable
{
    private readonly string _tempDirectory;
    private readonly SpatialSpecPersistence _persistence;

    public SpatialSpecPersistenceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), $"oravey2-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDirectory);
        _persistence = new SpatialSpecPersistence(_tempDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    private TownSpatialSpecification CreateTestSpecification()
    {
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var placements = new Dictionary<string, BuildingPlacement>
        {
            ["Cathedral"] = new BuildingPlacement("Cathedral", 52.5, 4.9, 40.0, 50.0, 45.0, "square_corner")
        };
        var network = new RoadNetwork(
            new List<Vector2> { new Vector2(52.5f, 4.9f) },
            new List<RoadEdge> { new RoadEdge(52.5, 4.9, 52.4, 4.8) },
            10.0f
        );
        var waters = new List<SpatialWaterBody>
        {
            new SpatialWaterBody("Canal", new List<Vector2> { new Vector2(52.3f, 4.7f) }, SpatialWaterType.Canal)
        };

        return new TownSpatialSpecification(bbox, placements, network, waters, "flat");
    }

    [Fact]
    public void Constructor_CreatesDirectory_WhenNotExists()
    {
        // Arrange
        var newDir = Path.Combine(Path.GetTempPath(), $"oravey2-new-{Guid.NewGuid():N}");
        Assert.False(Directory.Exists(newDir));

        // Act
        var persistence = new SpatialSpecPersistence(newDir);

        // Assert
        Assert.True(Directory.Exists(newDir));

        // Cleanup
        Directory.Delete(newDir, recursive: true);
    }

    [Fact]
    public void Constructor_DefaultDirectory_UsesOravey2Path()
    {
        // Act
        var persistence = new SpatialSpecPersistence();

        // Assert
        var expectedPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".oravey2",
            "town-specs"
        );
        Assert.Equal(expectedPath, persistence.SpecsDirectory);
    }

    [Fact]
    public async Task SaveToFileAsync_ValidSpec_SavesFile()
    {
        // Arrange
        var spec = CreateTestSpecification();
        var fileName = "test-spec.json";

        // Act
        await _persistence.SaveToFileAsync(fileName, spec);

        // Assert
        var filePath = Path.Combine(_tempDirectory, fileName);
        Assert.True(File.Exists(filePath));
    }

    [Fact]
    public async Task SaveToFileAsync_NullFileName_ThrowsArgumentNullException()
    {
        // Arrange
        var spec = CreateTestSpecification();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _persistence.SaveToFileAsync(null!, spec));
    }

    [Fact]
    public async Task SaveToFileAsync_EmptyFileName_ThrowsArgumentNullException()
    {
        // Arrange
        var spec = CreateTestSpecification();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _persistence.SaveToFileAsync("", spec));
    }

    [Fact]
    public async Task SaveToFileAsync_NullSpec_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _persistence.SaveToFileAsync("test.json", null!));
    }

    [Fact]
    public async Task LoadFromFileAsync_ExistingFile_ReturnsSpecification()
    {
        // Arrange
        var original = CreateTestSpecification();
        var fileName = "test-spec.json";
        await _persistence.SaveToFileAsync(fileName, original);

        // Act
        var loaded = await _persistence.LoadFromFileAsync(fileName);

        // Assert
        Assert.NotNull(loaded);
        Assert.Equal(original.TerrainDescription, loaded.TerrainDescription);
    }

    [Fact]
    public async Task LoadFromFileAsync_NonexistentFile_ThrowsFileNotFoundException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(() => _persistence.LoadFromFileAsync("nonexistent.json"));
    }

    [Fact]
    public async Task LoadFromFileAsync_NullFileName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _persistence.LoadFromFileAsync(null!));
    }

    [Fact]
    public async Task LoadFromFileAsync_EmptyFileName_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() => _persistence.LoadFromFileAsync(""));
    }

    [Fact]
    public async Task SaveAndLoad_RoundTrip_PreservesData()
    {
        // Arrange
        var original = CreateTestSpecification();
        var fileName = "roundtrip-test.json";

        // Act
        await _persistence.SaveToFileAsync(fileName, original);
        var loaded = await _persistence.LoadFromFileAsync(fileName);

        // Assert
        Assert.Equal(original.RealWorldBounds.MinLat, loaded.RealWorldBounds.MinLat);
        Assert.Equal(original.RealWorldBounds.MaxLat, loaded.RealWorldBounds.MaxLat);
        Assert.Equal(original.BuildingPlacements.Count, loaded.BuildingPlacements.Count);
        Assert.Equal(original.TerrainDescription, loaded.TerrainDescription);
    }

    [Fact]
    public async Task SaveToFileAsync_MultipleFiles_AllCreated()
    {
        // Arrange
        var spec1 = CreateTestSpecification();
        var spec2 = new TownSpatialSpecification(
            new BoundingBox(51.0, 52.0, 3.0, 4.0),
            new Dictionary<string, BuildingPlacement>(),
            new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f),
            new List<SpatialWaterBody>(),
            "hilly"
        );

        // Act
        await _persistence.SaveToFileAsync("spec1.json", spec1);
        await _persistence.SaveToFileAsync("spec2.json", spec2);

        // Assert
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "spec1.json")));
        Assert.True(File.Exists(Path.Combine(_tempDirectory, "spec2.json")));
    }

    [Fact]
    public async Task LoadFromFileAsync_InvalidJson_ThrowsInvalidOperationException()
    {
        // Arrange
        var fileName = "invalid.json";
        var filePath = Path.Combine(_tempDirectory, fileName);
        await File.WriteAllTextAsync(filePath, "not valid json at all");

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => _persistence.LoadFromFileAsync(fileName));
    }

    [Fact]
    public async Task SaveToFileAsync_OverwritesExistingFile()
    {
        // Arrange
        var fileName = "overwrite-test.json";
        var spec1 = CreateTestSpecification();
        var spec2 = new TownSpatialSpecification(
            new BoundingBox(51.0, 52.0, 3.0, 4.0),
            new Dictionary<string, BuildingPlacement>(),
            new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f),
            new List<SpatialWaterBody>(),
            "hilly"
        );

        // Act
        await _persistence.SaveToFileAsync(fileName, spec1);
        var firstLoad = await _persistence.LoadFromFileAsync(fileName);

        await _persistence.SaveToFileAsync(fileName, spec2);
        var secondLoad = await _persistence.LoadFromFileAsync(fileName);

        // Assert
        Assert.Equal("flat", firstLoad.TerrainDescription);
        Assert.Equal("hilly", secondLoad.TerrainDescription);
    }

    [Fact]
    public async Task SaveAndLoad_LargeSpecification_Success()
    {
        // Arrange
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var placements = new Dictionary<string, BuildingPlacement>();
        for (int i = 0; i < 50; i++)
        {
            placements[$"Building{i}"] = new BuildingPlacement(
                Name: $"Building{i}",
                CenterLat: 52.0 + (i * 0.001),
                CenterLon: 4.0 + (i * 0.001),
                WidthMeters: 20.0 + i,
                DepthMeters: 30.0 + i,
                RotationDegrees: (i * 3.6) % 360,
                AlignmentHint: "residential"
            );
        }

        var nodes = new List<Vector2>();
        for (int i = 0; i < 30; i++)
        {
            nodes.Add(new Vector2(52.0f + i * 0.01f, 4.0f + i * 0.01f));
        }

        var edges = new List<RoadEdge>();
        for (int i = 0; i < nodes.Count - 1; i++)
        {
            edges.Add(new RoadEdge(nodes[i].X, nodes[i].Y, nodes[i + 1].X, nodes[i + 1].Y));
        }

        var network = new RoadNetwork(nodes, edges, 10.0f);
        var spec = new TownSpatialSpecification(bbox, placements, network, new List<SpatialWaterBody>(), "mountainous");

        // Act
        await _persistence.SaveToFileAsync("large-spec.json", spec);
        var loaded = await _persistence.LoadFromFileAsync("large-spec.json");

        // Assert
        Assert.Equal(50, loaded.BuildingPlacements.Count);
        Assert.Equal(30, loaded.RoadNetwork.Nodes.Count);
        Assert.Equal(29, loaded.RoadNetwork.Edges.Count);
    }

    [Fact]
    public async Task SaveToFileAsync_FileContainsValidJson()
    {
        // Arrange
        var spec = CreateTestSpecification();
        var fileName = "valid-json-test.json";

        // Act
        await _persistence.SaveToFileAsync(fileName, spec);
        var content = await File.ReadAllTextAsync(Path.Combine(_tempDirectory, fileName));

        // Assert
        Assert.NotEmpty(content);
        Assert.Contains("\"version\"", content);
        Assert.Contains("\"realWorldBounds\"", content);
    }

    [Fact]
    public void SpecsDirectory_ReturnsConfiguredPath()
    {
        // Assert
        Assert.Equal(_tempDirectory, _persistence.SpecsDirectory);
    }

    [Fact]
    public async Task SaveAndLoad_SpecWithNoBuildings_Success()
    {
        // Arrange
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var network = new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f);
        var spec = new TownSpatialSpecification(bbox, new Dictionary<string, BuildingPlacement>(), network, new List<SpatialWaterBody>(), "flat");

        // Act
        await _persistence.SaveToFileAsync("empty-buildings.json", spec);
        var loaded = await _persistence.LoadFromFileAsync("empty-buildings.json");

        // Assert
        Assert.Empty(loaded.BuildingPlacements);
    }

    [Fact]
    public async Task SaveAndLoad_SpecWithNoRoads_Success()
    {
        // Arrange
        var bbox = new BoundingBox(52.0, 53.0, 4.0, 5.0);
        var network = new RoadNetwork(new List<Vector2>(), new List<RoadEdge>(), 10.0f);
        var placements = new Dictionary<string, BuildingPlacement> { ["Test"] = new("Test", 52.5, 4.9, 20, 20, 0, "test") };
        var spec = new TownSpatialSpecification(bbox, placements, network, new List<SpatialWaterBody>(), "flat");

        // Act
        await _persistence.SaveToFileAsync("no-roads.json", spec);
        var loaded = await _persistence.LoadFromFileAsync("no-roads.json");

        // Assert
        Assert.Empty(loaded.RoadNetwork.Nodes);
        Assert.Empty(loaded.RoadNetwork.Edges);
        Assert.NotEmpty(loaded.BuildingPlacements);
    }
}
