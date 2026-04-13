using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class LayoutStepView : ContentView
{
    public LayoutStepView(LayoutStepViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
        
        // TODO: Lambda subscription to PropertyChanged is not unsubscribed — potential event handler leak.
        // Leaving as-is due to MAUI view lifecycle complexity.
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(LayoutStepViewModel.SpatialTransform))
            {
                VisualizationControl.SetSpatialTransform(viewModel.SpatialTransform);
            }
        };
    }
}
