using CommunityToolkit.Maui.Storage;
using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.ViewModels.RegionTemplate;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.App.Views;

public partial class RegionTemplateView : ContentPage
{
    private readonly RegionTemplateMapDrawable _drawable = new();
    private PointF _panStart;
    private string _activeTab = "Towns";

    public RegionTemplateView()
    {
        InitializeComponent();
        BindingContext = App.Current?.Handler?.MauiContext?.Services.GetService<RegionTemplateViewModel>();

        MapCanvas.Drawable = _drawable;
        MapCanvas.EndInteraction += OnMapClicked;

        if (BindingContext is RegionTemplateViewModel vm)
        {
            vm.LoadPresetsFromRegions();

            vm.MapInvalidated += () => MainThread.BeginInvokeOnMainThread(() =>
            {
                RefreshMap();
                UpdateFooters();
            });
            vm.PropertyChanged += (_, args) =>
            {
                if (args.PropertyName == nameof(RegionTemplateViewModel.SelectedPreset) && vm.SelectedPreset is not null)
                {
                    _drawable.NorthLat = vm.SelectedPreset.NorthLat;
                    _drawable.SouthLat = vm.SelectedPreset.SouthLat;
                    _drawable.EastLon = vm.SelectedPreset.EastLon;
                    _drawable.WestLon = vm.SelectedPreset.WestLon;
                    _drawable.ResetGeoMapper();
                }
            };
        }
    }

    private void RefreshMap()
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;

        _drawable.ElevationGrid = vm.ElevationGrid;
        _drawable.Towns = vm.Towns;
        _drawable.Roads = vm.Roads;
        _drawable.WaterBodies = vm.WaterBodies;
        MapCanvas.Invalidate();
    }

    private void UpdateFooters()
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;
        TownFooter.Text = $"{vm.Towns.Count(t => t.IsIncluded)} of {vm.Towns.Count} included";
        RoadFooter.Text = $"{vm.Roads.Count(r => r.IsIncluded)} of {vm.Roads.Count} included";
        WaterFooter.Text = $"{vm.WaterBodies.Count(w => w.IsIncluded)} of {vm.WaterBodies.Count} included";
    }

    // --- Feature tab switching ---

    private void OnFeatureTabTowns(object? sender, EventArgs e) => SetActiveTab("Towns");
    private void OnFeatureTabRoads(object? sender, EventArgs e) => SetActiveTab("Roads");
    private void OnFeatureTabWater(object? sender, EventArgs e) => SetActiveTab("Water");

    private void SetActiveTab(string tab)
    {
        _activeTab = tab;
        TownList.IsVisible = tab == "Towns";
        RoadList.IsVisible = tab == "Roads";
        WaterList.IsVisible = tab == "Water";

        var active = Color.FromArgb("#512BD4");
        var inactive = Color.FromArgb("#2D2D3D");
        BtnTabTowns.BackgroundColor = tab == "Towns" ? active : inactive;
        BtnTabRoads.BackgroundColor = tab == "Roads" ? active : inactive;
        BtnTabWater.BackgroundColor = tab == "Water" ? active : inactive;

        SearchEntry.Text = string.Empty;
    }

    // --- Search ---

    private void OnSearchChanged(object? sender, TextChangedEventArgs e)
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;
        var filter = e.NewTextValue?.Trim() ?? string.Empty;

        if (_activeTab == "Towns")
        {
            if (string.IsNullOrEmpty(filter))
                TownList.ItemsSource = vm.Towns;
            else
                TownList.ItemsSource = vm.Towns.Where(t =>
                    t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else if (_activeTab == "Roads")
        {
            if (string.IsNullOrEmpty(filter))
                RoadList.ItemsSource = vm.Roads;
            else
                RoadList.ItemsSource = vm.Roads.Where(r =>
                    r.Classification.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    r.NearTown.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
        else
        {
            if (string.IsNullOrEmpty(filter))
                WaterList.ItemsSource = vm.WaterBodies;
            else
                WaterList.ItemsSource = vm.WaterBodies.Where(w =>
                    w.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    w.Type.Contains(filter, StringComparison.OrdinalIgnoreCase)).ToList();
        }
    }

    // --- List selection → map highlight ---

    private void OnTownSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;
        foreach (var t in vm.Towns) t.IsSelected = false;
        if (e.CurrentSelection.FirstOrDefault() is TownItem selected)
            selected.IsSelected = true;
        MapCanvas.Invalidate();
    }

    private void OnRoadSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;
        foreach (var r in vm.Roads) r.IsSelected = false;
        if (e.CurrentSelection.FirstOrDefault() is RoadItem selected)
            selected.IsSelected = true;
        MapCanvas.Invalidate();
    }

    private void OnWaterSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;
        foreach (var w in vm.WaterBodies) w.IsSelected = false;
        if (e.CurrentSelection.FirstOrDefault() is WaterItem selected)
            selected.IsSelected = true;
        MapCanvas.Invalidate();
    }

    // --- Auto-Cull dialog ---

    private async void OnAutoCull(object? sender, EventArgs e)
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;

        var dialogVm = new AutoCullDialogViewModel(vm.CullSettings, vm.Towns, vm.Roads, vm.WaterBodies);
        var dialog = new AutoCullDialog(dialogVm);

        await Navigation.PushModalAsync(dialog);

        // Wait for dialog to close
        var tcs = new TaskCompletionSource();
        dialogVm.CloseRequested += () => tcs.TrySetResult();
        await tcs.Task;

        if (dialogVm.Applied && dialogVm.Result is not null)
        {
            vm.CullSettings = dialogVm.Result;
            vm.AutoCull();
            UpdateFooters();
        }
    }

    // --- Layer toggles ---

    private void OnLayerToggle(object? sender, CheckedChangedEventArgs e)
    {
        _drawable.ShowTowns = ChkTowns.IsChecked;
        _drawable.ShowRoads = ChkRoads.IsChecked;
        _drawable.ShowWater = ChkWater.IsChecked;
        MapCanvas.Invalidate();
    }

    // --- Pan ---

    private void OnMapPan(object? sender, PanUpdatedEventArgs e)
    {
        switch (e.StatusType)
        {
            case GestureStatus.Started:
                _panStart = _drawable.Offset;
                break;
            case GestureStatus.Running:
                _drawable.Offset = new PointF(
                    _panStart.X + (float)e.TotalX,
                    _panStart.Y + (float)e.TotalY);
                MapCanvas.Invalidate();
                break;
        }
    }

    // --- Click-to-select ---

    private void OnMapClicked(object? sender, TouchEventArgs e)
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;
        if (e.Touches.Length == 0) return;

        var point = e.Touches[0];
        float w = (float)MapCanvas.Width;
        float h = (float)MapCanvas.Height;

        // Clear previous selection
        foreach (var t in vm.Towns) t.IsSelected = false;
        foreach (var r in vm.Roads) r.IsSelected = false;
        foreach (var wb in vm.WaterBodies) wb.IsSelected = false;

        // Priority: town > road > water
        var town = _drawable.HitTestTown(point, w, h);
        if (town is not null)
        {
            town.IsSelected = true;
            MapCanvas.Invalidate();
            return;
        }

        var road = _drawable.HitTestRoad(point, w, h);
        if (road is not null)
        {
            road.IsSelected = true;
            MapCanvas.Invalidate();
            return;
        }

        var water = _drawable.HitTestWater(point, w, h);
        if (water is not null)
        {
            water.IsSelected = true;
            MapCanvas.Invalidate();
        }
    }

    // --- Browse buttons ---

    private async void OnBrowseRegion(object? sender, EventArgs e)
    {
        if (BindingContext is not RegionTemplateViewModel vm) return;

        var geofabrikService = App.Current?.Handler?.MauiContext?.Services.GetService<IGeofabrikService>();
        if (geofabrikService is null) return;

        var pickerVm = new RegionPickerViewModel(geofabrikService);
        var dialog = new RegionPickerDialog(pickerVm);

        var tcs = new TaskCompletionSource<RegionPreset?>();
        pickerVm.RegionSelected += preset => tcs.TrySetResult(preset);
        pickerVm.Cancelled += () => tcs.TrySetResult(null);

        await Navigation.PushModalAsync(dialog);
        var selected = await tcs.Task;

        if (selected is not null)
            vm.ApplyRegionPreset(selected);
    }

    private async void OnBrowseSrtm(object? sender, EventArgs e)
    {
        var result = await FolderPicker.Default.PickAsync(CancellationToken.None);
        if (result?.Folder?.Path is string path && BindingContext is RegionTemplateViewModel vm)
            vm.SrtmDirectory = path;
    }

    private async void OnBrowseOsm(object? sender, EventArgs e)
    {
        var options = new PickOptions
        {
            PickerTitle = "Select OSM PBF file",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".osm.pbf", ".pbf"] }
            })
        };
        var result = await FilePicker.Default.PickAsync(options);
        if (result?.FullPath is string path && BindingContext is RegionTemplateViewModel vm)
            vm.OsmFilePath = path;
    }

    private async void OnBrowseOutput(object? sender, EventArgs e)
    {
        var options = new PickOptions
        {
            PickerTitle = "Select output path",
            FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
            {
                { DevicePlatform.WinUI, [".RegionTemplateFile"] }
            })
        };
        var result = await FilePicker.Default.PickAsync(options);
        if (result?.FullPath is string path && BindingContext is RegionTemplateViewModel vm)
            vm.OutputPath = path;
    }
}
