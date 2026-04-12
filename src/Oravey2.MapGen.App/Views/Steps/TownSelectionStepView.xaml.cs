using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class TownSelectionStepView : ContentView
{
    private readonly TownSelectionStepViewModel _viewModel;
    private readonly TownSelectionMapDrawable _mapDrawable;

    public TownSelectionStepView(TownSelectionStepViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();

        _mapDrawable = new TownSelectionMapDrawable();
        MapCanvas.Drawable = _mapDrawable;
        _viewModel.MapInvalidated += OnMapInvalidated;
    }

    private void OnMapInvalidated()
    {
        _mapDrawable.Towns = _viewModel.Towns;
        _mapDrawable.NorthLat = _viewModel.RegionNorthLat;
        _mapDrawable.SouthLat = _viewModel.RegionSouthLat;
        _mapDrawable.EastLon = _viewModel.RegionEastLon;
        _mapDrawable.WestLon = _viewModel.RegionWestLon;
        MapCanvas.Invalidate();
    }

    // --- Tab handlers ---

    private void OnListTab(object? sender, EventArgs e)
    {
        _viewModel.IsListTab = true;
        ListTabBtn.Style = (Style)Application.Current!.Resources["PrimaryButton"];
        MapTabBtn.Style = (Style)Application.Current.Resources["SecondaryButton"];
    }

    private void OnMapTab(object? sender, EventArgs e)
    {
        _viewModel.IsListTab = false;
        ListTabBtn.Style = (Style)Application.Current!.Resources["SecondaryButton"];
        MapTabBtn.Style = (Style)Application.Current.Resources["PrimaryButton"];
    }

    // --- Sort handlers ---

    private void OnSortByName(object? sender, EventArgs e)
    {
        _viewModel.SortMode = _viewModel.SortMode switch
        {
            TownSortMode.NameAsc => TownSortMode.NameDesc,
            TownSortMode.NameDesc => TownSortMode.None,
            _ => TownSortMode.NameAsc,
        };
    }

    private void OnSortBySize(object? sender, EventArgs e)
    {
        _viewModel.SortMode = _viewModel.SortMode switch
        {
            TownSortMode.SizeAsc => TownSortMode.SizeDesc,
            TownSortMode.SizeDesc => TownSortMode.None,
            _ => TownSortMode.SizeAsc,
        };
    }

    // --- Town card handlers ---

    private void OnEditToggle(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is TownSelectionItem item)
            item.IsEditing = !item.IsEditing;
    }

    private async void OnReroll(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is TownSelectionItem item)
            await _viewModel.RerollTownAsync(item);
    }

    private void OnDeleteTown(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is TownSelectionItem item)
            _viewModel.DeleteTown(item);
    }

    private async void OnAddTown(object? sender, EventArgs e)
    {
        if (Application.Current?.Windows.FirstOrDefault()?.Page is not Page page) return;
        if (!_viewModel.HasParsedTemplate)
        {
            _viewModel.StatusText = "Error: No parsed template available. Complete step 3 first.";
            return;
        }

        var available = _viewModel.GetAvailableOsmTowns();

        if (available.Count == 0)
        {
            _viewModel.StatusText = "No more towns available to add.";
            return;
        }

        var choices = available.Select(t => $"{t.Name} ({t.Category}, pop {t.Population:N0})").ToArray();
        var selected = await page.DisplayActionSheetAsync("Select Town", "Cancel", null, choices);

        if (string.IsNullOrEmpty(selected) || selected == "Cancel") return;

        var index = Array.IndexOf(choices, selected);
        if (index < 0) return;

        await _viewModel.AddTownFromOsm(available[index]);
    }
}
