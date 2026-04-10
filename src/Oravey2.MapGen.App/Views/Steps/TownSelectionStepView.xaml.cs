using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class TownSelectionStepView : ContentView
{
    private readonly TownSelectionStepViewModel _viewModel;

    public TownSelectionStepView(TownSelectionStepViewModel viewModel)
    {
        _viewModel = viewModel;
        BindingContext = viewModel;
        InitializeComponent();
    }

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
}
