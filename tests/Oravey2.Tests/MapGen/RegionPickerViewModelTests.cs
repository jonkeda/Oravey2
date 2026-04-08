using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.WorldTemplate;

namespace Oravey2.Tests.MapGen;

public class RegionPickerViewModelTests
{
    private static string MakeGeoJson()
    {
        var polygon = """{"type":"Polygon","coordinates":[[[4.0,52.0],[5.0,52.0],[5.0,53.0],[4.0,53.0],[4.0,52.0]]]}""";
        return """
        {
            "type": "FeatureCollection",
            "features": [
                {"type":"Feature","properties":{"id":"europe","name":"Europe"},"geometry":null},
                {"type":"Feature","properties":{"id":"netherlands","name":"Netherlands","parent":"europe","iso3166-1:alpha2":["NL"]},"geometry":null},
                {"type":"Feature","properties":{"id":"noord-holland","name":"Noord-Holland","parent":"netherlands","urls":{"pbf":"https://download.geofabrik.de/europe/netherlands/noord-holland-latest.osm.pbf"}},"geometry":POLYGON_PLACEHOLDER},
                {"type":"Feature","properties":{"id":"zuid-holland","name":"Zuid-Holland","parent":"netherlands","urls":{"pbf":"https://download.geofabrik.de/europe/netherlands/zuid-holland-latest.osm.pbf"}},"geometry":POLYGON_PLACEHOLDER},
                {"type":"Feature","properties":{"id":"africa","name":"Africa"},"geometry":null}
            ]
        }
        """.Replace("POLYGON_PLACEHOLDER", polygon);
    }

    private class FakeGeofabrikService : IGeofabrikService
    {
        private readonly string _json;
        public FakeGeofabrikService(string json) => _json = json;
        public Task<GeofabrikIndex> GetIndexAsync(bool forceRefresh = false) =>
            Task.FromResult(GeofabrikIndex.Parse(_json));
    }

    [Fact]
    public async Task LoadIndex_PopulatesFlatItems()
    {
        var vm = new RegionPickerViewModel(new FakeGeofabrikService(MakeGeoJson()));
        vm.LoadIndexCommand.Execute(null);
        // Wait for async command
        await Task.Delay(100);

        // Should show roots (depth 0)
        Assert.True(vm.FlatItems.Count >= 2); // Africa, Europe
        Assert.Contains(vm.FlatItems, i => i.Region.Id == "europe");
        Assert.Contains(vm.FlatItems, i => i.Region.Id == "africa");
    }

    [Fact]
    public async Task ToggleExpand_ShowsChildren()
    {
        var vm = new RegionPickerViewModel(new FakeGeofabrikService(MakeGeoJson()));
        vm.LoadIndexCommand.Execute(null);
        await Task.Delay(100);

        var europe = vm.FlatItems.First(i => i.Region.Id == "europe");
        vm.ToggleExpand(europe);

        Assert.True(europe.IsExpanded);
        Assert.Contains(vm.FlatItems, i => i.Region.Id == "netherlands");
    }

    [Fact]
    public async Task Search_FiltersAndExpandsParents()
    {
        var vm = new RegionPickerViewModel(new FakeGeofabrikService(MakeGeoJson()));
        vm.LoadIndexCommand.Execute(null);
        await Task.Delay(100);

        vm.SearchText = "Noord";

        Assert.Contains(vm.FlatItems, i => i.Region.Id == "noord-holland");
        // Parents should be visible
        Assert.Contains(vm.FlatItems, i => i.Region.Id == "europe");
        Assert.Contains(vm.FlatItems, i => i.Region.Id == "netherlands");
        // Unrelated continent should not be visible
        Assert.DoesNotContain(vm.FlatItems, i => i.Region.Id == "africa");
    }

    [Fact]
    public async Task Select_FiresRegionSelected()
    {
        var vm = new RegionPickerViewModel(new FakeGeofabrikService(MakeGeoJson()));
        vm.LoadIndexCommand.Execute(null);
        await Task.Delay(100);

        // Expand to get to noord-holland
        var europe = vm.FlatItems.First(i => i.Region.Id == "europe");
        vm.ToggleExpand(europe);
        var nl = vm.FlatItems.First(i => i.Region.Id == "netherlands");
        vm.ToggleExpand(nl);

        var nh = vm.FlatItems.First(i => i.Region.Id == "noord-holland");
        vm.SelectedItem = nh;

        RegionPreset? result = null;
        vm.RegionSelected += preset => result = preset;
        vm.SelectCommand.Execute(null);

        Assert.NotNull(result);
        Assert.Equal("noord-holland", result!.Name);
        Assert.Equal("Noord-Holland", result.DisplayName);
    }

    [Fact]
    public async Task SelectedPath_ShowsBreadcrumb()
    {
        var vm = new RegionPickerViewModel(new FakeGeofabrikService(MakeGeoJson()));
        vm.LoadIndexCommand.Execute(null);
        await Task.Delay(100);

        var europe = vm.FlatItems.First(i => i.Region.Id == "europe");
        vm.ToggleExpand(europe);
        var nl = vm.FlatItems.First(i => i.Region.Id == "netherlands");
        vm.ToggleExpand(nl);
        var nh = vm.FlatItems.First(i => i.Region.Id == "noord-holland");
        vm.SelectedItem = nh;

        Assert.Contains("Europe", vm.SelectedPath);
        Assert.Contains("Netherlands", vm.SelectedPath);
        Assert.Contains("Noord-Holland", vm.SelectedPath);
    }
}
