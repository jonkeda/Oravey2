using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.ViewModels.RegionTemplate;

namespace Oravey2.MapGen.App.Views;

public partial class ParseStepView : ContentView
{
    private readonly ParseStepViewModel _viewModel;

    public ParseStepView(ParseStepViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();

        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ParseStepViewModel.ShowMapPreview))
            UpdateMapDrawable();
        if (e.PropertyName == nameof(ParseStepViewModel.FilteredTownCount) && _viewModel.ShowMapPreview)
            UpdateMapDrawable();
    }

    private void UpdateMapDrawable()
    {
        if (_viewModel.ShowMapPreview && _viewModel.ParsedTemplate is { } template)
        {
            var drawable = new RegionTemplateMapDrawable
            {
                ElevationGrid = template.ElevationGrid,
                Towns = template.Towns.Select(t => new TownItem(t)).ToList(),
                Roads = template.Roads.Select(r => new RoadItem(r)).ToList(),
                WaterBodies = template.WaterBodies.Select(w => new WaterItem(w)).ToList(),
                NorthLat = _viewModel.GetState().Region.NorthLat,
                SouthLat = _viewModel.GetState().Region.SouthLat,
                EastLon = _viewModel.GetState().Region.EastLon,
                WestLon = _viewModel.GetState().Region.WestLon,
            };
            MapGraphicsView.Drawable = drawable;
            MapGraphicsView.Invalidate();
        }
        else
        {
            MapGraphicsView.Drawable = null;
        }
    }
}
