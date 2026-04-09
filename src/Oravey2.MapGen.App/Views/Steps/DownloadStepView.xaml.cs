using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class DownloadStepView : ContentView
{
    public DownloadStepView(DownloadStepViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}
