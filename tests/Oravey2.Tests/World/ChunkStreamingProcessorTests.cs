using Oravey2.Core.Framework.Events;
using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ChunkStreamingProcessorTests
{
    private static (ChunkStreamingProcessor proc, EventBus bus) Setup(
        int width = 8, int height = 8, int gridRadius = 1)
    {
        var world = new WorldMapData(width, height);
        var bus = new EventBus();
        return (new ChunkStreamingProcessor(world, bus, gridRadius: gridRadius), bus);
    }

    [Fact]
    public void Update_FirstCall_LoadsGrid()
    {
        var (proc, _) = Setup();
        var (loaded, unloaded) = proc.Update(4, 4);
        Assert.Equal(9, loaded.Count); // 3×3
        Assert.Empty(unloaded);
    }

    [Fact]
    public void Update_SameChunk_NoChange()
    {
        var (proc, _) = Setup();
        proc.Update(4, 4);
        var (loaded, unloaded) = proc.Update(4, 4);
        Assert.Empty(loaded);
        Assert.Empty(unloaded);
    }

    [Fact]
    public void Update_MoveRight_Loads3Unloads3()
    {
        var (proc, _) = Setup();
        proc.Update(4, 4);
        var (loaded, unloaded) = proc.Update(5, 4);
        Assert.Equal(3, loaded.Count);
        Assert.Equal(3, unloaded.Count);
    }

    [Fact]
    public void Update_CornerChunk_ClampedGrid()
    {
        var (proc, _) = Setup();
        var (loaded, _) = proc.Update(0, 0);
        Assert.Equal(4, loaded.Count); // 2×2 at corner
    }

    [Fact]
    public void Update_PublishesLoadEvents()
    {
        var (proc, bus) = Setup();
        var count = 0;
        bus.Subscribe<ChunkLoadedEvent>(_ => count++);
        proc.Update(4, 4);
        Assert.Equal(9, count);
    }

    [Fact]
    public void Update_PublishesUnloadEvents()
    {
        var (proc, bus) = Setup();
        proc.Update(4, 4);

        var count = 0;
        bus.Subscribe<ChunkUnloadedEvent>(_ => count++);
        proc.Update(5, 4);
        Assert.Equal(3, count);
    }

    [Fact]
    public void LoadedChunks_TracksCurrentGrid()
    {
        var (proc, _) = Setup();
        proc.Update(4, 4);
        Assert.Equal(9, proc.LoadedChunks.Count);
    }

    [Fact]
    public void ForceLoad_ClearsAndReloads()
    {
        var (proc, bus) = Setup();
        proc.Update(4, 4);

        var unloadCount = 0;
        var loadCount = 0;
        bus.Subscribe<ChunkUnloadedEvent>(_ => unloadCount++);
        bus.Subscribe<ChunkLoadedEvent>(_ => loadCount++);

        proc.ForceLoad(4, 4);
        Assert.Equal(9, unloadCount);
        Assert.Equal(9, loadCount);
    }

    [Fact]
    public void ForceLoad_DifferentPosition()
    {
        var (proc, _) = Setup();
        proc.Update(4, 4);
        var loaded = proc.ForceLoad(7, 7);
        // Corner at (7,7) in 8×8 world → only (6,6),(6,7),(7,6),(7,7) = 4
        Assert.Equal(4, loaded.Count);
        Assert.Equal(7, proc.CurrentCenterX);
        Assert.Equal(7, proc.CurrentCenterY);
    }

    [Fact]
    public void Update_EdgeMoveLoadsCorrectChunks()
    {
        var (proc, _) = Setup();
        proc.Update(4, 4);
        var (loaded, _) = proc.Update(4, 5);
        // Moving down 1: loads row at y=6 (3 chunks), unloads row at y=3
        Assert.All(loaded, c => Assert.Equal(6, c.cy));
    }

    [Fact]
    public void CurrentCenter_UpdatesOnMove()
    {
        var (proc, _) = Setup();
        proc.Update(4, 4);
        Assert.Equal(4, proc.CurrentCenterX);
        Assert.Equal(4, proc.CurrentCenterY);
    }

    [Fact]
    public void Update_SmallWorld_2x2_LimitsGrid()
    {
        var (proc, _) = Setup(2, 2);
        var (loaded, _) = proc.Update(0, 0);
        Assert.Equal(4, loaded.Count); // all 4 chunks in 2×2 world
    }

    // --- Step 10 tests ---

    [Fact]
    public void ActiveGrid_5x5_Has25Chunks()
    {
        // 10×10 world with default 5×5 grid (radius 2)
        var (proc, _) = Setup(width: 10, height: 10, gridRadius: 2);
        var (loaded, _) = proc.Update(5, 5);
        Assert.Equal(25, loaded.Count);
    }

    [Fact]
    public void PlayerMoves_ChunksEnterAndExit()
    {
        var (proc, _) = Setup(width: 12, height: 12, gridRadius: 2);
        proc.Update(5, 5); // Initial 5×5 load

        var (loaded, unloaded) = proc.Update(6, 5); // Move right 1
        // Should load column at dx=+2 (5 chunks), unload column at old dx=-2 (5 chunks)
        Assert.Equal(5, loaded.Count);
        Assert.Equal(5, unloaded.Count);
        Assert.All(loaded, c => Assert.Equal(8, c.cx));   // new right column
        Assert.All(unloaded, c => Assert.Equal(3, c.cx)); // old left column
    }

    [Fact]
    public void MissingChunk_TriggersGeneration()
    {
        var world = new WorldMapData(10, 10);
        var bus = new EventBus();
        var generatedChunks = new List<(int, int)>();
        var generator = new TestChunkGenerator(generatedChunks);

        var proc = new ChunkStreamingProcessor(world, bus, generator: generator, gridRadius: 1);
        proc.Update(5, 5);

        // All 9 chunks should have been generated (none in WorldMapData)
        Assert.Equal(9, generatedChunks.Count);
    }

    private sealed class TestChunkGenerator : IChunkGenerator
    {
        private readonly List<(int, int)> _generated;

        public TestChunkGenerator(List<(int, int)> generated) => _generated = generated;

        public ChunkData Generate(int chunkX, int chunkY)
        {
            _generated.Add((chunkX, chunkY));
            return ChunkData.CreateDefault(chunkX, chunkY);
        }
    }
}
