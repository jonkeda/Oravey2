using System.ComponentModel;
using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class TownMapsStepView : ContentView
{
    private readonly TownMapsStepViewModel _viewModel;
    private readonly TownMapPreviewDrawable _drawable = new();

    public TownMapsStepView(TownMapsStepViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();

        MapPreviewGraphicsView.Drawable = _drawable;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private TownMapItem? _watchedTown;

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TownMapsStepViewModel.SelectedTown))
        {
            WatchSelectedTown();
            RefreshPreview();
        }
    }

    private void WatchSelectedTown()
    {
        if (_watchedTown is not null)
            _watchedTown.PropertyChanged -= OnTownPropertyChanged;

        _watchedTown = _viewModel.SelectedTown;

        if (_watchedTown is not null)
            _watchedTown.PropertyChanged += OnTownPropertyChanged;
    }

    private void OnTownPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TownMapItem.MapResult))
        {
            RefreshPreview();
        }
    }

    private void RefreshPreview()
    {
        _drawable.MapResult = _viewModel.SelectedTown?.MapResult;
        MapPreviewGraphicsView.Invalidate();
    }
}
