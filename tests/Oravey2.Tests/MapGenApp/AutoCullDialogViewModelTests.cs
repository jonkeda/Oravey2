using System.Numerics;
using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.ViewModels.RegionTemplate;
using Oravey2.MapGen.RegionTemplates;
using Xunit;

namespace Oravey2.Tests.MapGenApp;

public class AutoCullDialogViewModelTests
{
    private static readonly CullSettings Default = new()
    {
        TownMinCategory = TownCategory.Village,
        TownMinPopulation = 1_000,
        TownMaxCount = 30,
        RoadMinClass = RoadClass.Primary,
        WaterMinAreaKm2 = 0.1,
        RoadSimplifyGeometry = false
    };

    [Fact]
    public void LoadFrom_CopiesAllSettings()
    {
        var vm = CreateVm();

        Assert.Equal(TownCategory.Village, vm.TownMinCategory);
        Assert.Equal(1_000, vm.TownMinPopulation);
        Assert.Equal(30, vm.TownMaxCount);
        Assert.Equal(RoadClass.Primary, vm.RoadMinClass);
        Assert.Equal(0.1, vm.WaterMinAreaKm2);
    }

    [Fact]
    public void BuildSettings_RoundTrips()
    {
        var vm = CreateVm();
        vm.TownMinPopulation = 5_000;
        vm.RoadMinClass = RoadClass.Secondary;
        vm.WaterMinAreaKm2 = 0.5;

        var result = vm.BuildSettings();

        Assert.Equal(5_000, result.TownMinPopulation);
        Assert.Equal(RoadClass.Secondary, result.RoadMinClass);
        Assert.Equal(0.5, result.WaterMinAreaKm2);
    }

    [Fact]
    public void Preview_ShowsCorrectCounts()
    {
        var vm = CreateVm();
        vm.TownMinCategory = TownCategory.Town;
        vm.TownMinPopulation = 5_000;

        vm.Preview();

        Assert.Contains("towns", vm.PreviewText);
        Assert.Contains("roads", vm.PreviewText);
        Assert.Contains("water", vm.PreviewText);
    }

    [Fact]
    public void Apply_SetsResultAndApplied()
    {
        var vm = CreateVm();
        bool closeRequested = false;
        vm.CloseRequested += () => closeRequested = true;

        vm.ApplyCommand.Execute(null);

        Assert.True(vm.Applied);
        Assert.NotNull(vm.Result);
        Assert.True(closeRequested);
    }

    [Fact]
    public void Cancel_DoesNotSetApplied()
    {
        var vm = CreateVm();
        bool closeRequested = false;
        vm.CloseRequested += () => closeRequested = true;

        vm.CancelCommand.Execute(null);

        Assert.False(vm.Applied);
        Assert.Null(vm.Result);
        Assert.True(closeRequested);
    }

    private static AutoCullDialogViewModel CreateVm()
    {
        var towns = new List<TownItem>
        {
            new(new TownEntry("Big", 52.0, 4.0, 20_000, Vector2.Zero, TownCategory.Town)),
            new(new TownEntry("Small", 52.1, 4.1, 500, new Vector2(10_000, 0), TownCategory.Hamlet)),
            new(new TownEntry("City", 52.2, 4.2, 100_000, new Vector2(20_000, 0), TownCategory.City))
        };
        var roads = new List<RoadItem>
        {
            new(new RoadSegment(RoadClass.Primary, [Vector2.Zero, new Vector2(1000, 0)])),
            new(new RoadSegment(RoadClass.Residential, [new Vector2(5000, 0), new Vector2(6000, 0)]))
        };
        var water = new List<WaterItem>
        {
            new(new WaterBody(WaterType.Lake, [Vector2.Zero, new Vector2(1000, 0), new Vector2(1000, 1000)]))
        };

        return new AutoCullDialogViewModel(Default, towns, roads, water);
    }
}
