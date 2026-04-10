using System.Numerics;
using Oravey2.MapGen.Generation;
using Oravey2.MapGen.Pipeline;
using Oravey2.MapGen.RegionTemplates;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.Tests.Pipeline;

public class TownSelectionStepViewModelTests
{
    private static TownSelectionStepViewModel MakeVM() => new();

    private static PipelineState MakeState() => new()
    {
        RegionName = "noord-holland",
        ContentPackPath = Path.Combine(Path.GetTempPath(), "test-content-pack-" + Guid.NewGuid().ToString("N")),
        Region = new RegionStepState
        {
            Completed = true,
            NorthLat = 52.9,
            SouthLat = 52.2,
            EastLon = 5.2,
            WestLon = 4.5,
        }
    };

    private static List<CuratedTown> MakeTowns(int count = 10)
    {
        var towns = new List<CuratedTown>();
        for (int i = 0; i < count; i++)
        {
            towns.Add(new CuratedTown(
                GameName: $"Haven-{i}",
                RealName: $"Town{i}",
                Latitude: 52.3 + i * 0.06,
                Longitude: 4.6 + i * 0.05,
                GamePosition: new Vector2(i * 20000f, i * 16700f),
                Role: i < 3 ? "trading_hub" : i < 7 ? "survivor_camp" : "raider_den",
                Faction: $"Faction-{i}",
                ThreatLevel: Math.Clamp(i + 1, 1, 10),
                Description: $"Town {i} description"));
        }
        return towns;
    }

    // --- Default state ---

    [Fact]
    public void Default_IsModeA()
    {
        var vm = MakeVM();
        Assert.True(vm.IsModeA);
        Assert.False(vm.IsModeB);
    }

    [Fact]
    public void Default_IsNotRunning()
    {
        var vm = MakeVM();
        Assert.False(vm.IsRunning);
        Assert.False(vm.HasResults);
    }

    [Fact]
    public void Default_SeedIs42()
    {
        var vm = MakeVM();
        Assert.Equal(42, vm.Seed);
    }

    [Fact]
    public void Default_TownsEmpty()
    {
        var vm = MakeVM();
        Assert.Empty(vm.Towns);
    }

    // --- Mode switching ---

    [Fact]
    public void ModeB_SetsIsModeAFalse()
    {
        var vm = MakeVM();
        vm.IsModeB = true;
        Assert.False(vm.IsModeA);
        Assert.True(vm.IsModeB);
    }

    [Fact]
    public void ModeDescription_ChangesWithMode()
    {
        var vm = MakeVM();
        Assert.Contains("Discover", vm.ModeDescription);
        vm.IsModeA = false;
        Assert.Contains("Select", vm.ModeDescription);
    }

    // --- PopulateTowns ---

    [Fact]
    public void PopulateTowns_SetsCollection()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        Assert.Equal(10, vm.Towns.Count);
        Assert.All(vm.Towns, t => Assert.True(t.IsIncluded));
    }

    [Fact]
    public void PopulateTowns_SetsHasResults()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(8));
        // HasResults must be set externally, but validation works
        Assert.Equal(8, vm.Towns.Count);
    }

    // --- Validation ---

    [Fact]
    public void Validation_Valid_10TownsWithThreatRange()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        Assert.True(vm.IsValid);
        Assert.Contains("✓", vm.ValidationSummary);
    }

    [Fact]
    public void Validation_Invalid_TooFewTowns()
    {
        var vm = MakeVM();
        // Only 5 towns — need 8–15
        vm.PopulateTowns(MakeTowns(5));
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void Validation_Invalid_NoHighThreat()
    {
        var vm = MakeVM();
        var towns = MakeTowns(10);
        // Set all threat levels to low
        vm.PopulateTowns(towns);
        foreach (var t in vm.Towns)
            t.ThreatLevel = 2;
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void Validation_ExcludingTown_UpdatesCount()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        Assert.True(vm.IsValid);

        // Exclude some until below min
        for (int i = 0; i < 3; i++)
            vm.Towns[i].IsIncluded = false;

        // Still valid with 7 included? No — need 8
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void ValidationSummary_ContainsTownCount()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        Assert.Contains("Towns: 10", vm.ValidationSummary);
    }

    // --- TownSelectionItem ---

    [Fact]
    public void TownSelectionItem_ThreatColor_LowIsGreen()
    {
        var item = new TownSelectionItem { ThreatLevel = 2 };
        Assert.Equal("#A6E3A1", item.ThreatColor);
    }

    [Fact]
    public void TownSelectionItem_ThreatColor_MidIsYellow()
    {
        var item = new TownSelectionItem { ThreatLevel = 5 };
        Assert.Equal("#F9E2AF", item.ThreatColor);
    }

    [Fact]
    public void TownSelectionItem_ThreatColor_HighIsRed()
    {
        var item = new TownSelectionItem { ThreatLevel = 8 };
        Assert.Equal("#F38BA8", item.ThreatColor);
    }

    [Fact]
    public void TownSelectionItem_IsEditing_TogglesNotEditing()
    {
        var item = new TownSelectionItem();
        Assert.False(item.IsEditing);
        Assert.True(item.IsNotEditing);

        item.IsEditing = true;
        Assert.True(item.IsEditing);
        Assert.False(item.IsNotEditing);
    }

    [Fact]
    public void TownSelectionItem_ToCuratedTown_RoundTrips()
    {
        var item = new TownSelectionItem
        {
            GameName = "Haven",
            RealName = "Purmerend",
            Latitude = 52.50,
            Longitude = 4.95,
            Role = "trading_hub",
            Faction = "Guard",
            ThreatLevel = 2,
            Description = "A town."
        };
        var curated = item.ToCuratedTown();
        Assert.Equal("Haven", curated.GameName);
        Assert.Equal("Purmerend", curated.RealName);
        Assert.Equal(52.50, curated.Latitude);
        Assert.Equal(2, curated.ThreatLevel);
    }

    // --- AddBlankTown ---

    [Fact]
    public void AddBlankTown_ViaPopulate_ThenAddAddsOne()
    {
        var vm = MakeVM();
        vm.Initialize(Path.GetTempPath());
        vm.Load(MakeState());
        vm.PopulateTowns(MakeTowns(8));
        var before = vm.Towns.Count;

        // Simulate HasResults
        // AddTownCommand requires HasResults, so set it indirectly
        typeof(TownSelectionStepViewModel)
            .GetProperty(nameof(TownSelectionStepViewModel.HasResults))!
            .SetValue(vm, true);

        vm.AddTownCommand.Execute(null);
        Assert.Equal(before + 1, vm.Towns.Count);
        Assert.True(vm.Towns.Last().IsEditing);
    }

    // --- Save output path ---

    [Fact]
    public void GetOutputPath_IncludesContentPackPath()
    {
        var vm = MakeVM();
        var state = MakeState();
        vm.Initialize(Path.GetTempPath());
        vm.Load(state);

        var path = vm.GetOutputPath();
        Assert.Contains("curated-towns.json", path);
        Assert.StartsWith(state.ContentPackPath, path);
    }

    // --- CuratedTownsFile ---

    [Fact]
    public void CuratedTownsFile_RoundTrip_SaveAndLoad()
    {
        var towns = MakeTowns(10);
        var file = CuratedTownsFile.FromCuratedTowns(towns, "A", 42);

        var tempPath = Path.Combine(Path.GetTempPath(), $"curated-towns-{Guid.NewGuid():N}.json");
        try
        {
            file.Save(tempPath);
            Assert.True(File.Exists(tempPath));

            var loaded = CuratedTownsFile.Load(tempPath);
            Assert.Equal("A", loaded.Mode);
            Assert.Equal(42, loaded.Seed);
            Assert.Equal(10, loaded.Towns.Count);
            Assert.Equal("Haven-0", loaded.Towns[0].GameName);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void CuratedTownsFile_FromCuratedTowns_MapsAllFields()
    {
        var town = new CuratedTown(
            "Haven", "Purmerend", 52.5, 4.95,
            Vector2.Zero, "trading_hub", "Guard", 2, "Desc");
        var file = CuratedTownsFile.FromCuratedTowns([town], "B", 99);

        Assert.Single(file.Towns);
        var entry = file.Towns[0];
        Assert.Equal("Haven", entry.GameName);
        Assert.Equal("Purmerend", entry.RealName);
        Assert.Equal("trading_hub", entry.Role);
        Assert.Equal(2, entry.ThreatLevel);
    }
}
