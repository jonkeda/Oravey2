using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class LayoutStepView : ContentView
{
    public LayoutStepView(LayoutStepViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
        
        // Wire up the visualization control to the view model's spatial transform property
        viewModel.PropertyChanged += (sender, e) =>
        {
            if (e.PropertyName == nameof(LayoutStepViewModel.SpatialTransform))
            {
                VisualizationControl.SetSpatialTransform(viewModel.SpatialTransform);
            }
        };
    }
}
