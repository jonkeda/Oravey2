using Oravey2.MapGen.ViewModels;

namespace Oravey2.MapGen.App.Views;

public partial class TownDesignStepView : ContentView
{
    public TownDesignStepView(TownDesignStepViewModel viewModel)
    {
        BindingContext = viewModel;
        InitializeComponent();
    }
}
