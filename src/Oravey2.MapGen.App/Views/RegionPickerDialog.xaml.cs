using Oravey2.MapGen.ViewModels;
using Oravey2.MapGen.RegionTemplates;

namespace Oravey2.MapGen.App.Views;

public partial class RegionPickerDialog : ContentPage
{
    private readonly RegionPickerViewModel _vm;

    public RegionPickerDialog(RegionPickerViewModel vm)
    {
        _vm = vm;
        BindingContext = vm;
        InitializeComponent();

        _vm.RegionSelected += OnRegionSelected;
        _vm.Cancelled += OnCancelled;

        // Load the index
        _vm.LoadIndexCommand.Execute(null);
    }

    private async void OnRegionSelected(RegionPreset preset)
    {
        _vm.RegionSelected -= OnRegionSelected;
        _vm.Cancelled -= OnCancelled;
        await Navigation.PopModalAsync();
    }

    private async void OnCancelled()
    {
        _vm.RegionSelected -= OnRegionSelected;
        _vm.Cancelled -= OnCancelled;
        await Navigation.PopModalAsync();
    }

    private void OnExpandTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.Parent?.BindingContext is RegionTreeItem item)
            _vm.ToggleExpand(item);
    }

    private void OnItemTapped(object? sender, TappedEventArgs e)
    {
        if (sender is View view && view.Parent?.BindingContext is RegionTreeItem item)
            _vm.SelectedItem = item;
    }
}
