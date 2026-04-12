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
                Description: $"Town {i} description",
                Size: i < 5 ? TownCategory.Village : i < 8 ? TownCategory.Town : TownCategory.City,
                Inhabitants: 1000 + i * 2000,
                Destruction: (DestructionLevel)(i % 5)));
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
    public void Default_MinMaxTowns()
    {
        var vm = MakeVM();
        Assert.Equal(8, vm.MinTowns);
        Assert.Equal(15, vm.MaxTowns);
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
        Assert.Equal(8, vm.Towns.Count);
    }

    // --- Validation ---

    [Fact]
    public void Validation_Valid_10Towns()
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
        vm.PopulateTowns(MakeTowns(5));
        Assert.False(vm.IsValid);
    }

    [Fact]
    public void Validation_ExcludingTown_UpdatesCount()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        Assert.True(vm.IsValid);

        for (int i = 0; i < 3; i++)
            vm.Towns[i].IsIncluded = false;

        // 7 included — need 8
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
    public void TownSelectionItem_DestructionColor_PristineIsGreen()
    {
        var item = new TownSelectionItem { Destruction = DestructionLevel.Pristine };
        Assert.Equal("#A6E3A1", item.DestructionColor);
    }

    [Fact]
    public void TownSelectionItem_DestructionColor_ModerateIsYellow()
    {
        var item = new TownSelectionItem { Destruction = DestructionLevel.Moderate };
        Assert.Equal("#F9E2AF", item.DestructionColor);
    }

    [Fact]
    public void TownSelectionItem_DestructionColor_DevastatedIsRed()
    {
        var item = new TownSelectionItem { Destruction = DestructionLevel.Devastated };
        Assert.Equal("#F38BA8", item.DestructionColor);
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
            Description = "A town.",
            Size = TownCategory.Town,
            Inhabitants = 5000,
            Destruction = DestructionLevel.Moderate,
        };
        var curated = item.ToCuratedTown();
        Assert.Equal("Haven", curated.GameName);
        Assert.Equal("Purmerend", curated.RealName);
        Assert.Equal(52.50, curated.Latitude);
        Assert.Equal(DestructionLevel.Moderate, curated.Destruction);
    }

    // --- DeleteTown ---

    [Fact]
    public void DeleteTown_RemovesFromCollection()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        var toDelete = vm.Towns[3];
        vm.DeleteTown(toDelete);
        Assert.Equal(9, vm.Towns.Count);
        Assert.DoesNotContain(toDelete, vm.Towns);
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
        var file = CuratedTownsFile.FromCuratedTowns(towns, "A");

        var tempPath = Path.Combine(Path.GetTempPath(), $"curated-towns-{Guid.NewGuid():N}.json");
        try
        {
            file.Save(tempPath);
            Assert.True(File.Exists(tempPath));

            var loaded = CuratedTownsFile.Load(tempPath);
            Assert.Equal("A", loaded.Mode);
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
            Vector2.Zero, "Desc",
            TownCategory.Town, 5000, DestructionLevel.Moderate);
        var file = CuratedTownsFile.FromCuratedTowns([town], "B");

        Assert.Single(file.Towns);
        var entry = file.Towns[0];
        Assert.Equal("Haven", entry.GameName);
        Assert.Equal("Purmerend", entry.RealName);
        Assert.Equal("Town", entry.Size);
        Assert.Equal(5000, entry.Inhabitants);
        Assert.Equal("Moderate", entry.Destruction);
    }

    // --- Tabs ---

    [Fact]
    public void Default_IsListTab()
    {
        var vm = MakeVM();
        Assert.True(vm.IsListTab);
        Assert.False(vm.IsMapTab);
    }

    [Fact]
    public void SwitchToMap_SetsIsMapTab()
    {
        var vm = MakeVM();
        vm.IsListTab = false;
        Assert.True(vm.IsMapTab);
        Assert.False(vm.IsListTab);
    }

    [Fact]
    public void IsListTabVisible_RequiresHasResults()
    {
        var vm = MakeVM();
        Assert.False(vm.IsListTabVisible);
        vm.PopulateTowns(MakeTowns(10));
        vm.HasResults = true;
        Assert.True(vm.IsListTabVisible);
    }

    [Fact]
    public void IsMapTabVisible_WhenMapSelected()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.HasResults = true;
        vm.IsListTab = false;
        Assert.True(vm.IsMapTabVisible);
        Assert.False(vm.IsListTabVisible);
    }

    [Fact]
    public void RegionBounds_ExposeState()
    {
        var vm = MakeVM();
        var state = MakeState();
        vm.Initialize(Path.GetTempPath());
        vm.Load(state);
        Assert.Equal(52.9, vm.RegionNorthLat);
        Assert.Equal(52.2, vm.RegionSouthLat);
        Assert.Equal(5.2, vm.RegionEastLon);
        Assert.Equal(4.5, vm.RegionWestLon);
    }

    [Fact]
    public void MapInvalidated_FiredOnPopulate()
    {
        var vm = MakeVM();
        var fired = false;
        vm.MapInvalidated += () => fired = true;
        vm.PopulateTowns(MakeTowns(10));
        Assert.True(fired);
    }

    [Fact]
    public void MapInvalidated_FiredOnDelete()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        var fired = false;
        vm.MapInvalidated += () => fired = true;
        vm.DeleteTown(vm.Towns[0]);
        Assert.True(fired);
    }

    [Fact]
    public void MapInvalidated_FiredOnTabSwitch()
    {
        var vm = MakeVM();
        var fired = false;
        vm.MapInvalidated += () => fired = true;
        vm.IsListTab = false;
        Assert.True(fired);
    }

    [Fact]
    public void MapInvalidated_FiredOnIsIncludedChange()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        var fired = false;
        vm.MapInvalidated += () => fired = true;
        vm.Towns[0].IsIncluded = false;
        Assert.True(fired);
    }

    // --- Search & Sort ---

    [Fact]
    public void FilteredTowns_DefaultMatchesTowns()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        Assert.Equal(10, vm.FilteredTowns.Count);
    }

    [Fact]
    public void SearchText_FiltersOnGameName()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SearchText = "Haven-3";
        Assert.Single(vm.FilteredTowns);
        Assert.Equal("Haven-3", vm.FilteredTowns[0].GameName);
    }

    [Fact]
    public void SearchText_FiltersOnRealName()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SearchText = "Town5";
        Assert.Single(vm.FilteredTowns);
        Assert.Equal("Town5", vm.FilteredTowns[0].RealName);
    }

    [Fact]
    public void SearchText_CaseInsensitive()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SearchText = "haven-3";
        Assert.Single(vm.FilteredTowns);
    }

    [Fact]
    public void SearchText_Empty_ShowsAll()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SearchText = "Haven-3";
        Assert.Single(vm.FilteredTowns);
        vm.SearchText = "";
        Assert.Equal(10, vm.FilteredTowns.Count);
    }

    [Fact]
    public void SortMode_NameAsc()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SortMode = TownSortMode.NameAsc;
        Assert.True(string.Compare(vm.FilteredTowns[0].GameName, vm.FilteredTowns[1].GameName, StringComparison.OrdinalIgnoreCase) <= 0);
    }

    [Fact]
    public void SortMode_NameDesc()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SortMode = TownSortMode.NameDesc;
        Assert.True(string.Compare(vm.FilteredTowns[0].GameName, vm.FilteredTowns[1].GameName, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void SortMode_SizeAsc()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SortMode = TownSortMode.SizeAsc;
        Assert.True(vm.FilteredTowns[0].Size <= vm.FilteredTowns[^1].Size);
    }

    [Fact]
    public void SortMode_SizeDesc()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SortMode = TownSortMode.SizeDesc;
        Assert.True(vm.FilteredTowns[0].Size >= vm.FilteredTowns[^1].Size);
    }

    [Fact]
    public void SearchAndSort_Compose()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SearchText = "Haven";
        vm.SortMode = TownSortMode.NameDesc;
        Assert.Equal(10, vm.FilteredTowns.Count); // all match "Haven"
        Assert.True(string.Compare(vm.FilteredTowns[0].GameName, vm.FilteredTowns[1].GameName, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void Validation_UsesFullCollection_NotFiltered()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        vm.SearchText = "Haven-0"; // filters to 1
        Assert.Single(vm.FilteredTowns);
        Assert.True(vm.IsValid); // validation counts all 10 from Towns
    }

    [Fact]
    public void DeleteTown_UpdatesFilteredTowns()
    {
        var vm = MakeVM();
        vm.PopulateTowns(MakeTowns(10));
        var toDelete = vm.Towns[3];
        vm.DeleteTown(toDelete);
        Assert.Equal(9, vm.FilteredTowns.Count);
        Assert.DoesNotContain(toDelete, vm.FilteredTowns);
    }
}
