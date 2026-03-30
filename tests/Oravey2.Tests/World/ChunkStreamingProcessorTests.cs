using Oravey2.Core.Framework.Events;
using Oravey2.Core.World;

namespace Oravey2.Tests.World;

public class ChunkStreamingProcessorTests
{
    private static (ChunkStreamingProcessor proc, EventBus bus) Setup(int width = 8, int height = 8)
    {
        var world = new WorldMapData(width, height);
        var bus = new EventBus();
        return (new ChunkStreamingProcessor(world, bus), bus);
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
}
